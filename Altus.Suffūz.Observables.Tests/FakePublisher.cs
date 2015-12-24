using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public class FakePublisher : IPublisher
    {
        public Operation LastPropertyUpdate { get; private set; }
        public Operation LastMethodCalled { get; private set; }
        public Operation LastCreated { get; private set; }
        public Operation LastDisposed { get; private set; }

        public void Publish<T>(Created<T> created) where T : class, new()
        {
            this.LastCreated = created;
        }

        public void Publish<T>(Disposed<T> disposed) where T : class, new()
        {
            this.LastDisposed = disposed;
        }

        public void Publish<T, U>(PropertyUpdate<T, U> update) where T : class, new()
        {
            this.LastPropertyUpdate = update;
        }

        public void Publish<T, U>(MethodCall<T, U> called) where T : class, new()
        {
            this.LastMethodCalled = called;
        }


    }
}
