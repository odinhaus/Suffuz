using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{ 
    public abstract class Subscription : IDisposable
    {
       
        public event EventHandler Disposed;

        protected Subscription(SubscriptionConfig config)
        {
            this.Configuration = config;
        }
        public SubscriptionConfig Configuration { get; private set; }


        public void Dispose()
        {
            if (Disposed != null)
            {
                Disposed(this, new EventArgs());
            }
        }

        public void Notify<T>(Created<T> created) where T : class, new()
        {
            created.Subscription = (Subscription<T>)this;
            var actions = Configuration.GetHandlers(created);
            foreach(var action in actions)
            {
                action(created);
            }
        }

        public void Notify<T>(Disposed<T> disposed) where T : class, new()
        {
            disposed.Subscription = (Subscription<T>)this;
            var actions = Configuration.GetHandlers(disposed);
            foreach (var action in actions)
            {
                action(disposed);
            }
        }

        public void Notify<T, U>(PropertyUpdate<T, U> changed) where T : class, new()
        {
            changed.Subscription = (Subscription<T>)this;
            var actions = Configuration.GetHandlers(changed);
            foreach (var action in actions)
            {
                action(changed);
            }
        }

        public void Notify<T, U>(MethodCall<T, U> called) where T : class, new()
        {
            called.Subscription = (Subscription<T>)this;
            var actions = Configuration.GetHandlers(called);
            foreach (var action in actions)
            {
                action(called);
            }
        }

        public void Notify<T>(AnyOperation<T> called) where T : class, new()
        {
            called.Subscription = (Subscription<T>)this;
            var actions = Configuration.GetHandlers(called);
            foreach (var action in actions)
            {
                action(called);
            }
        }
    }

    public class Subscription<T> : Subscription where T : class, new()
    {
        public Subscription(SubscriptionConfig<T> config) : base(config)
        {}
    }
}
