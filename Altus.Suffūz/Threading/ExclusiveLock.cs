using Altus.Suffūz.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Altus.Suffūz.Threading
{
    public class ExclusiveLock
    {
        string _name;
        int _count;

        public ExclusiveLock(string name)
        {
            _name = name;
        }

        public void Enter([CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            //Logger.LogInfo("Thread {1} Entering {0} Lock, Count {2}, Member {3}: line {4} {5}", _name, Thread.CurrentThread.ManagedThreadId, _count, memberName, sourceLineNumber, Path.GetFileName(sourceFilePath));
            Monitor.Enter(this);
            Interlocked.Increment(ref _count);
            string space = "".PadLeft(_count, ' ');
            Logger.LogInfo(space + "Thread {1} Entered {0} Lock, Count {2}, Member {3}: line {4} {5}", _name, Thread.CurrentThread.ManagedThreadId, _count, memberName, sourceLineNumber, Path.GetFileName(sourceFilePath));
        }

        public void Exit([CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            //Logger.LogInfo("Thread {1} Exiting {0} Lock, Count {2}, Member {3}: line {4} {5}", _name, Thread.CurrentThread.ManagedThreadId, _count, memberName, sourceLineNumber, Path.GetFileName(sourceFilePath));
            Interlocked.Decrement(ref _count);
            string space = "".PadLeft(_count, ' ');
            Monitor.Exit(this);
            Logger.LogInfo(space + "Thread {1} Exited {0} Lock, Count {2}, Member {3}: line {4} {5}", _name, Thread.CurrentThread.ManagedThreadId, _count, memberName, sourceLineNumber, Path.GetFileName(sourceFilePath));
        }

        public void Lock(Action action, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            try
            {
                Enter(memberName, sourceFilePath, sourceLineNumber);
                action();
            }
            finally
            {
                Exit(memberName, sourceFilePath, sourceLineNumber);
            }
        }

        public T Lock<T>(Func<T> func, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            try
            {
                Enter(memberName, sourceFilePath, sourceLineNumber);
                return func();
            }
            finally
            {
                Exit(memberName, sourceFilePath, sourceLineNumber);
            }
        }
    }
}
