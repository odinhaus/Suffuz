using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.Collections;

namespace Altus.Suffūz.Scheduling
{
    public class Scheduler : IScheduler
    {
        List<IScheduledTask> _tasks = new List<IScheduledTask>();
        Thread _taskRunner;
        bool _running = false;

        public event TaskExpiredHandler TaskExpired;

        static Scheduler _current;
        public static Scheduler Current
        {
            get
            {
                if (_current == null)
                {
                    _current = new Scheduler();
                    _current.Initialize();
                }
                return _current;
            }
        }

        private Scheduler() { }

        protected void Initialize()
        {
            _tasks.AddRange(App.ResolveAll<IScheduledTask>());
            _running = true;

            _taskRunner = new Thread(new ThreadStart(RunTasks));
            _taskRunner.IsBackground = true;
            _taskRunner.Name = "Scheduled Task Scheduler";
            _taskRunner.Start();
        }

        Dictionary<IScheduledTask, TaskRunner> _runners = new Dictionary<IScheduledTask, TaskRunner>();
        private void RunTasks()
        {
            while (_running)
            {
                IScheduledTask[] tasks;
                DateTime now = DateTime.Now;
                lock(this)
                {
                    tasks = _tasks.ToArray();
                }

                for (int i = 0; i < tasks.Length; i++)
                {
                    IScheduledTask task = tasks[i];

                    if (!_runners.ContainsKey(task))
                    {
                        if (task.Schedule[now].IsScheduled(now.Ticks))
                        {
                            if (task.Schedule is FixedCalendricalSchedule && task.Schedule.Interval == 0)
                            {
                                // these are single-fire tasks, so just execute them
                                ThreadPool.QueueUserWorkItem((state) =>
                                {
                                    var t = (IScheduledTask)state;
                                    Scheduler._currentTask = t;
                                    t.Execute(t.ExecuteArgs());
                                    Scheduler._currentTask = null;
                                }, task);
                                lock (this)
                                {
                                    _tasks.Remove(task);
                                }
                            }
                            else
                            {
                                _runners.Add(task, new TaskRunner(task));
                            }
                        }
                        else if (task.Schedule.Interval < 0)
                        {
                            // it's been canceled before it was scheduled to execute
                            lock (this)
                            {
                                _tasks.Remove(task);
                            }
                        }
                    }

                    if (_runners.ContainsKey(task) && _runners[task].IsExpired)
                    {
                        OnTaskExpired(task);
                        _runners.Remove(task);
                        lock(this)
                        {
                            _tasks.Remove(task);
                        }
                    }
                }

                Thread.Sleep(1);
            }
        }

        protected virtual void OnTaskExpired(IScheduledTask task)
        {
            if (this.TaskExpired != null)
            {
                TaskExpired(this, new TaskExpiredEventArgs(task));
            }
        }

        [ThreadStatic]
        static IScheduledTask _currentTask = null;
        public IScheduledTask CurrentTask { get { return _currentTask; } }

        private List<IScheduledTask> Tasks
        {
            get
            {
                return _tasks;
            }
        }

        private class TaskRunner
        {
            public TaskRunner(IScheduledTask task)
            {
                Task = task;
                IsExpired = !task.Schedule[DateTime.Now].IsScheduled(DateTime.Now.Ticks);

                Thread thread = new System.Threading.Thread(new ParameterizedThreadStart(RunTask));
                thread.IsBackground = true;
                thread.Name = "Scheduled Task Runner " + task.GetType().Name;
                thread.Start();
                Thread = thread;
            }

            private void RunTask(object args)
            {
                Stopwatch sw = new Stopwatch();
                long elapsed = 0;
                sw.Start();
                Scheduler._currentTask = Task;
                while (!IsExpired)
                {
                    Task.Execute(Task.ExecuteArgs());
                    int interval = Task.Schedule[DateTime.Now].Interval;
                    if (interval > 0)
                    {
                        int sleep = Math.Max(0, interval - (int)(sw.ElapsedMilliseconds - elapsed));
                        Thread.Sleep(sleep);
                        elapsed = sw.ElapsedMilliseconds;
                    }
                    IsExpired = !(interval >= 0 && Task.Schedule[DateTime.Now].IsScheduled(DateTime.Now.Ticks));
                }
            }

            public IScheduledTask Task { get; private set; }
            public Thread Thread { get; private set; }
            public bool IsExpired { get; private set; }
        }

        public IEnumerator<IScheduledTask> GetEnumerator()
        {
            lock(this)
            {
                foreach (var task in Tasks.ToArray())
                    yield return task;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        /// <summary>
        /// Schedules a task for execution
        /// </summary>
        /// <param name="task"></param>
        public void Schedule(IScheduledTask task)
        {
            lock (Scheduler.Current)
            {
                Scheduler.Current.Tasks.Add(task);
            }
        }

        /// <summary>
        /// Schedules an action for execution
        /// </summary>
        /// <param name="schedule">the action's schedule</param>
        /// <param name="action">the action to execute</param>
        /// <returns></returns>
        public IScheduledTask Schedule(Schedule schedule, Action action)
        {
            var task = new ScheduledDelegate(schedule, action);
            lock (Scheduler.Current)
            {
                Scheduler.Current.Tasks.Add(task);
            }
            return task;
        }

        /// <summary>
        /// Schedules an action to be executed every interval milliseconds
        /// </summary>
        /// <param name="interval">the execution period in milliseconds</param>
        /// <param name="action">the action to execute</param>
        /// <returns></returns>
        public IScheduledTask Schedule(int interval, Action action)
        {
            var task = new ScheduledDelegate(interval, action);
            lock(Scheduler.Current)
            {
                Scheduler.Current.Tasks.Add(task);
            }
            return task;
        }

        public IScheduledTask Schedule<T>(Schedule schedule, Action<T> action, Func<T> args)
        {
            var task = new ScheduledDelegate(schedule, action, () => new object[] { args() });
            lock (Scheduler.Current)
            {
                Scheduler.Current.Tasks.Add(task);
            }
            return task;
        }

        public IScheduledTask Schedule<T>(DateTime when, Action<T> action, Func<T> args)
        {
            var task = new ScheduledDelegate(when, action, () => new object[] { args() });
            lock (Scheduler.Current)
            {
                Scheduler.Current.Tasks.Add(task);
            }
            return task;
        }

        public IScheduledTask Schedule<T>(int interval, Action<T> action, Func<T> args)
        {
            var task = new ScheduledDelegate(interval, action, () => new object[] { args() });
            lock (Scheduler.Current)
            {
                Scheduler.Current.Tasks.Add(task);
            }
            return task;
        }

        public IScheduledTask Schedule<T>(Schedule schedule, Func<T> action)
        {
            var task = new ScheduledDelegate(schedule, action);
            lock (Scheduler.Current)
            {
                Scheduler.Current.Tasks.Add(task);
            }
            return task;
        }

        public IScheduledTask Schedule<T>(int interval, Func<T> action)
        {
            var task = new ScheduledDelegate(interval, action);
            lock (Scheduler.Current)
            {
                Scheduler.Current.Tasks.Add(task);
            }
            return task;
        }

        public IScheduledTask Schedule<T>(DateTime when, Func<T> action)
        {
            var task = new ScheduledDelegate(when, action);
            lock (Scheduler.Current)
            {
                Scheduler.Current.Tasks.Add(task);
            }
            return task;
        }

        public IScheduledTask Schedule<T, U>(Schedule schedule, Func<T, U> action, Func<U> args)
        {
            var task = new ScheduledDelegate(schedule, action, () => new object[] { args() });
            lock (Scheduler.Current)
            {
                Scheduler.Current.Tasks.Add(task);
            }
            return task;
        }

        public IScheduledTask Schedule<T, U>(int interval, Func<T, U> action, Func<U> args)
        {
            var task = new ScheduledDelegate(interval, action, () => new object[] { args() });
            lock (Scheduler.Current)
            {
                Scheduler.Current.Tasks.Add(task);
            }
            return task;
        }


        public IScheduledTask Schedule<T, U>(DateTime when, Func<T, U> action, Func<U> args)
        {
            var task = new ScheduledDelegate(when, action, () => new object[] { args() });
            lock (Scheduler.Current)
            {
                Scheduler.Current.Tasks.Add(task);
            }
            return task;
        }


        #region IDisposable Members
        bool disposed = false;

        // Implement IDisposable.
        // Do not make this method virtual.
        // A derived class should not be able to override this method.
        public void Dispose()
        {
            Dispose(true);
            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SupressFinalize to
            // take this object off the finalization queue 
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        public event EventHandler Disposing;
        public event EventHandler Disposed;
        //========================================================================================================//
        // Dispose(bool disposing) executes in two distinct scenarios.
        // If disposing equals true, the method has been called directly
        // or indirectly by a user's code. Managed and unmanaged resources
        // can be disposed.
        // If disposing equals false, the method has been called by the 
        // runtime from inside the finalizer and you should not reference 
        // other objects. Only unmanaged resources can be disposed.
        private void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!this.disposed)
            {
                if (this.Disposing != null)
                    this.Disposing(this, new EventArgs());
                // If disposing equals true, dispose all managed 
                // and unmanaged resources.
                if (disposing)
                {
                    // Dispose managed resources.
                    this.OnDisposeManagedResources();
                }

                // Call the appropriate methods to clean up 
                // unmanaged resources here.
                // If disposing is false, 
                // only the following code is executed.
                this.OnDisposeUnmanagedResources();
                if (this.Disposed != null)
                    this.Disposed(this, new EventArgs());
            }
            disposed = true;
        }

        /// <summary>
        /// Dispose managed resources
        /// </summary>
        protected virtual void OnDisposeManagedResources()
        {
            if (_taskRunner != null)
            {
                _running = false;
                if (!_taskRunner.Join(2000))
                    _taskRunner.Abort();
            }
        }

        /// <summary>
        /// Dispose unmanaged (native resources)
        /// </summary>
        protected virtual void OnDisposeUnmanagedResources()
        {
        }

        #endregion
    }
}
