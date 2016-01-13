using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public class SubscriptionManager : IManageSubscriptions
    {
        static List<Subscription> _subscriptions = new List<Subscription>();

        public void Add(Subscription subscription)
        {
            lock(_subscriptions)
            {
                _subscriptions.Add(subscription);
                subscription.Disposed += Subscription_Disposed;
            }
        }

        private void Subscription_Disposed(object sender, EventArgs e)
        {
            Remove((Subscription)sender);
        }

        public bool Remove(Subscription subscription)
        {
            lock (_subscriptions)
            {
                return _subscriptions.Remove(subscription);
            }
        }

        public IEnumerator<Subscription> GetEnumerator()
        {
            lock(_subscriptions)
            {
                return _subscriptions.ToArray()
                    .AsEnumerable().GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public void Notify<T>(Created<T> created) where T : class, new()
        {
            lock(_subscriptions)
            {
                foreach (var subscription in _subscriptions)
                {
                    subscription.Notify(created);
                }
                var any = new AnyOperation<T>(
                    created.GlobalKey,
                    created.OperationState,
                    created.OperationMode,
                    created.MemberName,
                    created.Instance,
                    created.EventClass,
                    created.EventOrder,
                    created);
                Notify(any);
            }
        }

        public void Notify<T>(Disposed<T> disposed) where T : class, new()
        {
            lock (_subscriptions)
            {
                foreach (var subscription in _subscriptions)
                {
                    subscription.Notify(disposed);
                }
                var any = new AnyOperation<T>(
                    disposed.GlobalKey,
                    disposed.OperationState,
                    disposed.OperationMode,
                    disposed.MemberName,
                    disposed.Instance,
                    disposed.EventClass,
                    disposed.EventOrder,
                    disposed);
                Notify(any);
            }
        }

        public void Notify<T, U>(PropertyUpdate<T, U> changed) where T : class, new()
        {
            lock (_subscriptions)
            {
                foreach (var subscription in _subscriptions)
                {
                    subscription.Notify(changed);
                }
                var any = new AnyOperation<T>(
                    changed.GlobalKey,
                    changed.OperationState,
                    changed.OperationMode,
                    changed.MemberName,
                    changed.Instance,
                    changed.EventClass,
                    changed.EventOrder,
                    changed);
                Notify(any);
            }
        }

        public void Notify<T, U>(MethodCall<T, U> called) where T : class, new()
        {
            lock (_subscriptions)
            {
                foreach (var subscription in _subscriptions)
                {
                    subscription.Notify(called);
                }
                var any = new AnyOperation<T>(
                    called.GlobalKey,
                    called.OperationState,
                    called.OperationMode,
                    called.MemberName,
                    called.Instance,
                    called.EventClass,
                    called.EventOrder,
                    called);
                Notify(any);
            }
        }

        public void Notify<T>(AnyOperation<T> any) where T : class, new()
        {
            lock (_subscriptions)
            {
                foreach (var subscription in _subscriptions)
                {
                    subscription.Notify(any);
                }
            }
        }
    }
}
