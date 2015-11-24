using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz
{
    public class Observable<T> : IObservable<Observing<T>>
    {
        internal Observable(string globalKey)
        {
            GlobalKey = globalKey;
        }
        public string GlobalKey { get; private set; }
        public T Observed { get; private set; }
        public string ChannelId { get; set; }
        public Observing<T> Subscribe(IObserver<Observing<T>> observer)
        {
            throw new NotImplementedException();
        }
        public Observing<T> Subscribe(Action<T> onNext = null, Action<Exception> onError = null, Action onCompleted = null)
        {
            throw new NotImplementedException();
        }

        IDisposable IObservable<Observing<T>>.Subscribe(IObserver<Observing<T>> observer)
        {
            return Subscribe(observer);
        }
    }

    public class Observing<T> : IDisposable
    {
        public string GlobalKey { get; private set; }
        public T Observed { get; private set; }
        public string ChannelId { get; set; }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public static implicit operator T(Observing<T> observed)
        {
            return observed.Observed;
        }
    }
}
