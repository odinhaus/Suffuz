using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public class Publisher : IPublisher
    {
        public void Publish<T>(Created<T> created) where T : class, new()
        {
            
        }

        public void Publish<T>(Disposed<T> disposed) where T : class, new()
        {

        }

        public void Publish<T, U>(PropertyUpdate<T, U> created) where T : class, new()
        {

        }

        public void Publish<T, U>(MethodCall<T, U> created) where T : class, new()
        {

        }
    }
}
