using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public interface IManageSubscriptions : IEnumerable<Subscription>
    {
        void Add(Subscription subscription);
        bool Remove(Subscription subscription);
        void Notify<T>(Created<T> created) where T : class, new();
        void Notify<T>(Disposed<T> disposed) where T : class, new();
        void Notify<T, U>(PropertyUpdate<T, U> changed) where T : class, new();
        void Notify<T, U>(MethodCall<T, U> called) where T : class, new();
    }
}
