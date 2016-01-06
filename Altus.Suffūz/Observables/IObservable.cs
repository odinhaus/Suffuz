using Altus.Suffūz.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public interface IObservable<T> where T : class, new()
    {
        string GlobalKey { get; }
        T Instance { get; }
        ExclusiveLock SyncLock { get; }
        IPublisher Publisher { get; }
    }
}
