using System;
using System.Collections.Generic;

namespace Altus.Suffūz.Scheduling
{
    public delegate void TaskExpiredHandler(object sender, TaskExpiredEventArgs e);
    public class TaskExpiredEventArgs : EventArgs
    {
        public TaskExpiredEventArgs(IScheduledTask task)
        {
            this.Task = task;
        }

        public IScheduledTask Task { get; private set; }
    }

    public interface IScheduler : IEnumerable<IScheduledTask>, IDisposable
    {
        event TaskExpiredHandler TaskExpired;
        void Schedule(IScheduledTask task);
        IScheduledTask Schedule(Schedule schedule, Action action);
        IScheduledTask Schedule(int interval, Action action);
        IScheduledTask Schedule<T>(Schedule schedule, Func<T> action);
        IScheduledTask Schedule<T>(int interval, Func<T> action);
        IScheduledTask Schedule<T>(DateTime when, Func<T> action);
        IScheduledTask Schedule<T>(int interval, Action<T> action, Func<T> args);
        IScheduledTask Schedule<T>(Schedule schedule, Action<T> action, Func<T> args);
        IScheduledTask Schedule<T>(DateTime when, Action<T> action, Func<T> args);
        IScheduledTask Schedule<T, U>(Schedule schedule, Func<T, U> action, Func<U> args);
        IScheduledTask Schedule<T, U>(int interval, Func<T, U> action, Func<U> args);
        IScheduledTask Schedule<T, U>(DateTime when, Func<T, U> action, Func<U> args);
    }
}