using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public class SubscriptionConfig<T> where T : class, new()
    {
        /// <summary>
        /// Subscribes to instance creation event, before the instance is created.  This will only be called once per globally unique id for type T, 
        /// until that instance is disposed by all subscribers.
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeCreated(Expression<Action<Created<T>>> subscriber)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to instance creation event, before the instance is created for the specific instance associated with key.  
        /// This will only be called once per globally unique id for type T, until that instance is disposed by all subscribers.
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <param name="key">the key of the instance to subscribe to</param>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeCreated(Expression<Action<Created<T>>> subscriber, string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to instance creation event, before the instance is created for the specific instance(s) determined by the instanceSelector predicate.  
        /// This will only be called once per globally unique id for type T,  until that instance is disposed by all subscribers.
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <param name="instanceSelector">a predicate to evaluate to determine which events the subscriber should handle</param>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeCreated(Expression<Action<Created<T>>> subscriber, Func<Created<T>, bool> instanceSelector)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to instance creation event, after the instance is created
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterCreated(Expression<Action<Created<T>>> subscriber)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to instance creation event, after the instance is created for the specific instance associated with key.  
        /// This will only be called once per globally unique id for type T, until that instance is disposed by all subscribers.
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <param name="key">the key of the instance to subscribe to</param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterCreated(Expression<Action<Created<T>>> subscriber, string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to instance creation event, after the instance is created for the specific instance(s) determined by the instanceSelector predicate.  
        /// This will only be called once per globally unique id for type T,  until that instance is disposed by all subscribers.
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <param name="instanceSelector">a predicate to evaluate to determine which events the subscriber should handle</param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterCreated(Expression<Action<Created<T>>> subscriber, Func<Created<T>, bool> instanceSelector)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to instance disposal event, before the instance is disposed
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeDisposed(Expression<Action<Disposed<T>>> subscriber)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to instance disposal event, before the instance is disposed, for the specified instance key.
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeDisposed(Expression<Action<Disposed<T>>> subscriber, string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to instance disposal event, before the instance is disposed, for the instances matching the instanceSelector
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instanceSelector"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeDisposed(Expression<Action<Disposed<T>>> subscriber, Func<Disposed<T>, bool> instanceSelector)
        {
            throw new NotImplementedException();
        }

        

        /// <summary>
        /// Subscribes to instance disposal event, after the instance is disposed
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterDisposed(Expression<Action<Disposed<T>>> subscriber)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to instance disposal event, after the instance is disposed, for the specified instance key.
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterDisposed(Expression<Action<Disposed<T>>> subscriber, string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to instance disposal event, after the instance is disposed, for the instances matching the instanceSelector
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instanceSelector"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterDisposed(Expression<Action<Disposed<T>>> subscriber, Func<Disposed<T>, bool> instanceSelector)
        {
            throw new NotImplementedException();
        }

        

        /// <summary>
        /// Subscribes to any property update or method call, before they are applied
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeAny(Expression<Action<AnyOperation<T>>> subscriber)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to any property update or method call, before they are applied for the instance specified by key
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeAny(Expression<Action<AnyOperation<T>>> subscriber, string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to any property update or method call, before they are applied for the instances matched by instanceSelector predicate
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instanceSelector"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeAny(Expression<Action<AnyOperation<T>>> subscriber, Func<AnyOperation<T>, bool> instanceSelector)
        {
            throw new NotImplementedException();
        }

        

        /// <summary>
        /// Subscribes to any property update or method call, after they are applied
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterAny(Expression<Action<AnyOperation<T>>> subscriber)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to any property update or method call, after they are applied for the instance specified by key
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterAny(Expression<Action<AnyOperation<T>>> subscriber, string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to any property update or method call, after they are applied for the instances matched by instanceSelector predicate
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instanceSelector"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterAny(Expression<Action<AnyOperation<T>>> subscriber, Func<AnyOperation<T>, bool> instanceSelector)
        {
            throw new NotImplementedException();
        }

        

        /// <summary>
        ///  Subscribes to a specific method call before it is called
        /// </summary>
        /// <param name="methodCalled">the method to subscribe to</param>
        /// <param name="subscriber">the handler to call</param>
        /// <typeparam name="U">return type</typeparam>
        /// <typeparam name="A">first argument type</typeparam>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeCalled<U, A>(Expression<Func<T, Func<A, U>>> methodCalled, Expression<Action<MethodCall<T, U>>> subscriber)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a specific method call before it is called for the instance specified by key
        /// </summary>
        /// <param name="methodCalled"></param>
        /// <param name="subscriber"></param>
        /// <param name="key"></param>
        /// <typeparam name="U">return type</typeparam>
        /// <typeparam name="A">first argument type</typeparam>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeCalled<U, A>(Expression<Func<T, Func<A, U>>> methodCalled, Expression<Action<MethodCall<T, U>>> subscriber, string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a specific method call before it is called for the instances specified by instancePredicate
        /// </summary>
        /// <param name="methodCalled"></param>
        /// <param name="subscriber"></param>
        /// <param name="instancePredicate"></param>
        /// <typeparam name="U">return type</typeparam>
        /// <typeparam name="A">first argument type</typeparam>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeCalled<U, A>(Expression<Func<T, Func<A, U>>> methodCalled, Expression<Action<MethodCall<T, U>>> subscriber, Func<MethodCall<T, U>, bool> instancePredicate)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///  Subscribes to a specific method call before it is called
        /// </summary>
        /// <param name="methodCalled">the method to subscribe to</param>
        /// <param name="subscriber">the handler to call</param>
        /// <typeparam name="U">return type</typeparam>
        /// <typeparam name="A">first argument type</typeparam>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeCalled<U, A, B>(Expression<Func<T, Func<A, B, U>>> methodCalled, Expression<Action<MethodCall<T, U>>> subscriber)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a specific method call before it is called for the instance specified by key
        /// </summary>
        /// <param name="methodCalled"></param>
        /// <param name="subscriber"></param>
        /// <param name="key"></param>
        /// <typeparam name="U">return type</typeparam>
        /// <typeparam name="A">first argument type</typeparam>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeCalled<U, A, B>(Expression<Func<T, Func<A, B, U>>> methodCalled, Expression<Action<MethodCall<T, U>>> subscriber, string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a specific method call before it is called for the instances specified by instancePredicate
        /// </summary>
        /// <param name="methodCalled"></param>
        /// <param name="subscriber"></param>
        /// <param name="instancePredicate"></param>
        /// <typeparam name="U">return type</typeparam>
        /// <typeparam name="A">first argument type</typeparam>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeCalled<U, A, B>(Expression<Func<T, Func<A, B, U>>> methodCalled, Expression<Action<MethodCall<T, U>>> subscriber, Func<MethodCall<T, U>, bool> instancePredicate)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// Subscribes to a specific method call after it is called
        /// </summary>
        /// <param name="method">the method to subscribe to</param>
        /// <param name="subscriber">the handler to call</param>
        /// <typeparam name="U">return type</typeparam>
        /// <typeparam name="A">first argument type</typeparam>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterCalled<U, A>(Expression<Func<T, Func<A, U>>> methodCalled, Expression<Action<MethodCall<T, U>>> subscriber)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Subscribes to a specific method call after it is called for the instance specified by key
        /// </summary>
        /// <param name="methodCalled"></param>
        /// <param name="subscriber"></param>
        /// <param name="key"></param>
        /// <typeparam name="U">return type</typeparam>
        /// <typeparam name="A">first argument type</typeparam>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterCalled<U, A>(Expression<Func<T, Func<A, U>>> methodCalled, Expression<Action<MethodCall<T, U>>> subscriber, string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a specific method call after it is called for the instances specified by instancePredicate
        /// </summary>
        /// <param name="methodCalled"></param>
        /// <param name="subscriber"></param>
        /// <param name="instancePredicate"></param>
        /// <typeparam name="U">return type</typeparam>
        /// <typeparam name="A">first argument type</typeparam>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterCalled<U, A>(Expression<Func<T, Func<A, U>>> methodCalled, Expression<Action<MethodCall<T, U>>> subscriber, Func<MethodCall<T, U>, bool> instancePredicate)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a specific method call after it is called
        /// </summary>
        /// <param name="method">the method to subscribe to</param>
        /// <param name="subscriber">the handler to call</param>
        /// <typeparam name="U">return type</typeparam>
        /// <typeparam name="A">first argument type</typeparam>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterCalled<U, A, B>(Expression<Func<T, Func<A, B, U>>> methodCalled, Expression<Action<MethodCall<T, U>>> subscriber)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Subscribes to a specific method call after it is called for the instance specified by key
        /// </summary>
        /// <param name="methodCalled"></param>
        /// <param name="subscriber"></param>
        /// <param name="key"></param>
        /// <typeparam name="U">return type</typeparam>
        /// <typeparam name="A">first argument type</typeparam>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterCalled<U, A, B>(Expression<Func<T, Func<A, B, U>>> methodCalled, Expression<Action<MethodCall<T, U>>> subscriber, string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a specific method call after it is called for the instances specified by instancePredicate
        /// </summary>
        /// <param name="methodCalled"></param>
        /// <param name="subscriber"></param>
        /// <param name="instancePredicate"></param>
        /// <typeparam name="U">return type</typeparam>
        /// <typeparam name="A">first argument type</typeparam>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterCalled<U, A, B>(Expression<Func<T, Func<A, B, U>>> methodCalled, Expression<Action<MethodCall<T, U>>> subscriber, Func<MethodCall<T, U>, bool> instancePredicate)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// Subscribes to a property value change, before the change is applied
        /// </summary>
        /// <typeparam name="U">the property type</typeparam>
        /// <param name="propertyChanged">the property to subscribe to</param>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeChanged<U>(Expression<Func<T, U>> propertyChanged, Expression<Action<PropertyUpdate<T, U>>> subscriber)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a property value change, before the change is applied for the instance specified by key
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <param name="propertyChanged"></param>
        /// <param name="subscriber"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeChanged<U>(Expression<Func<T, U>> propertyChanged, Expression<Action<PropertyUpdate<T, U>>> subscriber, string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a property value change, before the change is applied for the instances specified by instancePredicate
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <param name="propertyChanged"></param>
        /// <param name="subscriber"></param>
        /// <param name="instancePredicate"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeChanged<U>(Expression<Func<T, U>> propertyChanged, Expression<Action<PropertyUpdate<T, U>>> subscriber, Func<PropertyUpdate<T, U>, bool> instancePredicate)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a property value change, after the change is applied
        /// </summary>
        /// <typeparam name="U">the property type</typeparam>
        /// <param name="method">the property to subscribe to</param>
        /// <param name="subscriber">the handle to call</param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterChanged<U>(Expression<Func<T, U>> method, Expression<Action<PropertyUpdate<T, U>>> subscriber)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a property value change, after the change is applied for the instance specified by key
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <param name="propertyChanged"></param>
        /// <param name="subscriber"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterChanged<U>(Expression<Func<T, U>> propertyChanged, Expression<Action<PropertyUpdate<T, U>>> subscriber, string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a property value change, after the change is applied for the instances specified by instancePredicate
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <param name="propertyChanged"></param>
        /// <param name="subscriber"></param>
        /// <param name="instancePredicate"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterChanged<U>(Expression<Func<T, U>> propertyChanged, Expression<Action<PropertyUpdate<T, U>>> subscriber, Func<PropertyUpdate<T, U>, bool> instancePredicate)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates the subscription and starts listening for operations.  Disposing of the returned Subscription instance releases the subscription.
        /// </summary>
        /// <returns></returns>
        public Subscription<T> Subscribe()
        {
            throw new NotImplementedException();
        }
    }
}
