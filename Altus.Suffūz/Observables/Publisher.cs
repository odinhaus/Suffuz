using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public class Publisher : IPublisher
    {
        public Publisher(IManageSubscriptions subscriptions)
        {
            this.SubscriptionManager = subscriptions;
        }

        public IManageSubscriptions SubscriptionManager { get; private set; }

        public void Publish<T>(Created<T> created) where T : class, new()
        {
            this.SubscriptionManager.Notify(created);
        }

        public void Publish<T>(Disposed<T> disposed) where T : class, new()
        {
            this.SubscriptionManager.Notify(disposed);
        }

        public void Publish<T, U>(PropertyUpdate<T, U> created) where T : class, new()
        {
            this.SubscriptionManager.Notify(created);
        }

        public void Publish<T, U>(MethodCall<T, U> called) where T : class, new()
        {
            this.SubscriptionManager.Notify(called);
        }
    }
}
