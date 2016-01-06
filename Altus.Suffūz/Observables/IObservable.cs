using Altus.Suffūz.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public interface IObservable<T> : IObservable where T : class, new()
    {
        new T Instance { get; }
    }

    public interface IObservable
    {
        string GlobalKey { get; }
        object Instance { get; }
        ExclusiveLock SyncLock { get; }
        IPublisher Publisher { get; }
    }
}
