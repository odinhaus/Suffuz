using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Altus.Suffūz.Threading
{
    public class ExclusiveLock
    {
        string _name;

        public ExclusiveLock(string name)
        {
            _name = name;
        }

        public void Enter()
        {
            Monitor.Enter(this);
        }

        public void Exit()
        {
            Monitor.Exit(this);
        }

        public void Lock(Action action)
        {
            try
            {
                Enter();
                action();
            }
            finally
            {
                Exit();
            }
        }

        public T Lock<T>(Func<T> func)
        {
            try
            {
                Enter();
                return func();
            }
            finally
            {
                Exit();
            }
        }
    }
}
