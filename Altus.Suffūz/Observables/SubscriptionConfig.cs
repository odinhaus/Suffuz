using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public class SubscriptionSelector
    {
        public SubscriptionSelector(Func<Operation, bool> selectionPredicate, Delegate selectedAction)
        {
            Predicate = selectionPredicate;
            Action = selectedAction;
        }

        public Func<Operation, bool> Predicate { get; private set; }
        public Delegate Action { get; private set; }
    }

    public class SubscriptionConfig
    {
        List<SubscriptionSelector> _selectors = new List<SubscriptionSelector>();

        protected void AddSelector(SubscriptionSelector selector)
        {
            _selectors.Add(selector);
        }

        protected void RemoveSelector(SubscriptionSelector selector)
        {
            _selectors.Remove(selector);
        }

        internal IEnumerable<Action<Created<T>>> GetHandlers<T>(Created<T> created) where T : class, new()
        {
            return _selectors.Where(s => s.Predicate(created)).Select(s => (Action<Created<T>>)s.Action);
        }

        internal IEnumerable<Action<Disposed<T>>> GetHandlers<T>(Disposed<T> created) where T : class, new()
        {
            return _selectors.Where(s => s.Predicate(created)).Select(s => (Action<Disposed<T>>)s.Action);
        }

        internal IEnumerable<Action<PropertyUpdate<T, U>>> GetHandlers<T, U>(PropertyUpdate<T, U> created) where T : class, new()
        {
            return _selectors.Where(s => s.Predicate(created)).Select(s => (Action<PropertyUpdate<T, U>>)s.Action);
        }

        internal IEnumerable<Action<MethodCall<T, U>>> GetHandlers<T, U>(MethodCall<T, U> created) where T : class, new()
        {
            return _selectors.Where(s => s.Predicate(created)).Select(s => (Action<MethodCall<T, U>>)s.Action);
        }

        internal IEnumerable<Action<AnyOperation<T>>> GetHandlers<T>(AnyOperation<T> created) where T : class, new()
        {
            return _selectors.Where(s => s.Predicate(created)).Select(s => (Action<AnyOperation<T>>)s.Action);
        }
    }

    public class SubscriptionConfig<T> : SubscriptionConfig where T : class, new()
    {
        /// <summary>
        /// Subscribes to instance creation event, before the instance is created.  This will only be called once per globally unique id for type T, 
        /// until that instance is disposed by all subscribers.
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeCreated(Expression<Action<Created<T>>> subscriber)
        {
            Func<Operation, bool> predicate = (operation) => operation is Created<T>
                                                             && operation.OperationState == OperationState.Before
                                                             && operation.OperationMode == OperationMode.Created
                                                             && operation.InstanceType.Equals(typeof(T));
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
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
            Func<Operation, bool> predicate = (operation) => operation is Created<T>
                                                             && operation.OperationState == OperationState.Before
                                                             && operation.OperationMode == OperationMode.Created
                                                             && operation.InstanceType.Equals(typeof(T)) 
                                                             && operation.GlobalKey.Equals(key);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
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
            Func<Operation, bool> predicate = (operation) => operation is Created<T>
                                                             && operation.OperationState == OperationState.Before
                                                             && operation.OperationMode == OperationMode.Created
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && instanceSelector((Created<T>)operation);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
        }

        /// <summary>
        /// Subscribes to instance creation event, after the instance is created
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterCreated(Expression<Action<Created<T>>> subscriber)
        {
            Func<Operation, bool> predicate = (operation) => operation is Created<T>
                                                             && operation.OperationState == OperationState.After
                                                             && operation.OperationMode == OperationMode.Created
                                                             && operation.InstanceType.Equals(typeof(T));
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
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
            Func<Operation, bool> predicate = (operation) => operation is Created<T>
                                                             && operation.OperationState == OperationState.After
                                                             && operation.OperationMode == OperationMode.Created
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.GlobalKey.Equals(key);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
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
            Func<Operation, bool> predicate = (operation) => operation is Created<T>
                                                             && operation.OperationState == OperationState.After 
                                                             && operation.OperationMode == OperationMode.Created
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && instanceSelector((Created<T>)operation);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
        }

        /// <summary>
        /// Subscribes to instance disposal event, before the instance is disposed
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeDisposed(Expression<Action<Disposed<T>>> subscriber)
        {
            Func<Operation, bool> predicate = (operation) => operation is Disposed<T>
                                                             && operation.OperationState == OperationState.Before
                                                             && operation.OperationMode == OperationMode.Disposed
                                                             && operation.InstanceType.Equals(typeof(T));
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
        }

        /// <summary>
        /// Subscribes to instance disposal event, before the instance is disposed for the provided instance
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeDisposed(Expression<Action<Disposed<T>>> subscriber, T instance)
        {
            Func<Operation, bool> predicate = (operation) => operation is Disposed<T>
                                                             && operation.OperationState == OperationState.Before
                                                             && operation.OperationMode == OperationMode.Disposed
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.Instance == instance;
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
        }

        /// <summary>
        /// Subscribes to instance disposal event, before the instance is disposed, for the specified instance key.
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeDisposed(Expression<Action<Disposed<T>>> subscriber, string key)
        {
            Func<Operation, bool> predicate = (operation) => operation is Disposed<T>
                                                             && operation.OperationState == OperationState.Before
                                                             && operation.OperationMode == OperationMode.Disposed
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.GlobalKey.Equals(key);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
        }

        /// <summary>
        /// Subscribes to instance disposal event, before the instance is disposed, for the instances matching the instanceSelector
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instanceSelector"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeDisposed(Expression<Action<Disposed<T>>> subscriber, Func<Disposed<T>, bool> instanceSelector)
        {
            Func<Operation, bool> predicate = (operation) => operation is Disposed<T>
                                                             && operation.OperationState == OperationState.Before
                                                             && operation.OperationMode == OperationMode.Disposed
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && instanceSelector((Disposed<T>)operation);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
        }

        

        /// <summary>
        /// Subscribes to instance disposal event, after the instance is disposed
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterDisposed(Expression<Action<Disposed<T>>> subscriber)
        {
            Func<Operation, bool> predicate = (operation) => operation is Disposed<T>
                                                             && operation.OperationState == OperationState.After
                                                             && operation.OperationMode == OperationMode.Disposed
                                                             && operation.InstanceType.Equals(typeof(T));
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
        }

        /// <summary>
        /// Subscribes to instance disposal event, after the instance is disposed, for the specified instance key.
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterDisposed(Expression<Action<Disposed<T>>> subscriber, string key)
        {
            Func<Operation, bool> predicate = (operation) => operation is Disposed<T>
                                                             && operation.OperationState == OperationState.After
                                                             && operation.OperationMode == OperationMode.Disposed
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.GlobalKey.Equals(key);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
        }

        /// <summary>
        /// Subscribes to instance disposal event, after the instance is disposed, for the instances matching the instanceSelector
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instanceSelector"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterDisposed(Expression<Action<Disposed<T>>> subscriber, Func<Disposed<T>, bool> instanceSelector)
        {
            Func<Operation, bool> predicate = (operation) => operation is Disposed<T>
                                                             && operation.OperationState == OperationState.After
                                                             && operation.OperationMode == OperationMode.Disposed
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && instanceSelector((Disposed<T>)operation);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
        }

        /// <summary>
        /// Subscribes to instance disposal event, after the instance is disposed for the provided instance
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterDisposed(Expression<Action<Disposed<T>>> subscriber, T instance)
        {
            Func<Operation, bool> predicate = (operation) => operation is Disposed<T>
                                                             && operation.OperationState == OperationState.After
                                                             && operation.OperationMode == OperationMode.Disposed
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.Instance == instance;
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
        }

        /// <summary>
        /// Subscribes to any property update or method call, before they are applied
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeAny(Expression<Action<AnyOperation<T>>> subscriber)
        {
            Func<Operation, bool> predicate = (operation) => operation is AnyOperation<T>
                                                             && operation.OperationState == OperationState.Before
                                                             && operation.InstanceType.Equals(typeof(T));
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
        }

        /// <summary>
        /// Subscribes to any property update or method call, before they are applied for the instance specified by key
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeAny(Expression<Action<AnyOperation<T>>> subscriber, string key)
        {
            Func<Operation, bool> predicate = (operation) => operation is AnyOperation<T>
                                                             && operation.OperationState == OperationState.Before
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.GlobalKey.Equals(key);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
        }

        /// <summary>
        /// Subscribes to any property update or method call, before they are applied for the instances matched by instanceSelector predicate
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instanceSelector"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeAny(Expression<Action<AnyOperation<T>>> subscriber, Func<AnyOperation<T>, bool> instanceSelector)
        {
            Func<Operation, bool> predicate = (operation) => operation is AnyOperation<T>
                                                             && operation.OperationState == OperationState.Before
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && instanceSelector((AnyOperation<T>)operation);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
        }

        /// <summary>
        /// Subscribes to any property update or method call, before they are applied for the instance specified
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeAny(Expression<Action<AnyOperation<T>>> subscriber, T instance)
        {
            Func<Operation, bool> predicate = (operation) => operation is AnyOperation<T>
                                                             && operation.OperationState == OperationState.Before
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.Instance == instance;
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
        }

        /// <summary>
        /// Subscribes to any property update or method call, after they are applied
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterAny(Expression<Action<AnyOperation<T>>> subscriber) 
        {
            Func<Operation, bool> predicate = (operation) => operation is AnyOperation<T>
                                                             && operation.OperationState == OperationState.After
                                                             && operation.InstanceType.Equals(typeof(T));
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
        }

        /// <summary>
        /// Subscribes to any property update or method call, after they are applied for the instance specified by key
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterAny(Expression<Action<AnyOperation<T>>> subscriber, string key)
        {
            Func<Operation, bool> predicate = (operation) => operation is AnyOperation<T>
                                                             && operation.OperationState == OperationState.After
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.GlobalKey.Equals(key);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
        }

        /// <summary>
        /// Subscribes to any property update or method call, after they are applied for the instances matched by instanceSelector predicate
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instanceSelector"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterAny(Expression<Action<AnyOperation<T>>> subscriber, Func<AnyOperation<T>, bool> instanceSelector)
        {
            Func<Operation, bool> predicate = (operation) => operation is AnyOperation<T>
                                                             && operation.OperationState == OperationState.After
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && instanceSelector((AnyOperation<T>)operation);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
        }

        /// <summary>
        /// Subscribes to any property update or method call, after they are applied for the instance specified
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterAny(Expression<Action<AnyOperation<T>>> subscriber, T instance)
        {
            Func<Operation, bool> predicate = (operation) => operation is AnyOperation<T>
                                                             && operation.OperationState == OperationState.After
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.Instance == instance;
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
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
            Func<Operation, bool> predicate = (operation) => operation is MethodCall<T, U>
                                                             && operation.OperationState == OperationState.Before
                                                             && operation.OperationMode == OperationMode.MethodCall
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.MemberName.Equals(((MemberExpression)methodCalled.Body).Member.Name);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
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
            Func<Operation, bool> predicate = (operation) => operation is MethodCall<T, U>
                                                             && operation.OperationState == OperationState.Before
                                                             && operation.OperationMode == OperationMode.MethodCall
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.MemberName.Equals(((MemberExpression)methodCalled.Body).Member.Name)
                                                             && operation.GlobalKey.Equals(key);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
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
            Func<Operation, bool> predicate = (operation) => operation is MethodCall<T, U>
                                                             && operation.OperationState == OperationState.Before
                                                             && operation.OperationMode == OperationMode.MethodCall
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.MemberName.Equals(((MemberExpression)methodCalled.Body).Member.Name)
                                                             && instancePredicate((MethodCall<T,U>)operation);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
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
            Func<Operation, bool> predicate = (operation) => operation is MethodCall<T, U>
                                                             && operation.OperationState == OperationState.Before
                                                             && operation.OperationMode == OperationMode.MethodCall
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.MemberName.Equals(((MemberExpression)methodCalled.Body).Member.Name);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
        }

        /// <summary>
        /// Subscribes to a specific method call before it is called for the instance specified
        /// </summary>
        /// <param name="methodCalled"></param>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeCalled<U, A>(Expression<Func<T, Func<A, U>>> methodCalled, Expression<Action<MethodCall<T, U>>> subscriber, T instance)
        {
            Func<Operation, bool> predicate = (operation) => operation is MethodCall<T, U>
                                                             && operation.OperationState == OperationState.Before
                                                             && operation.OperationMode == OperationMode.MethodCall
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.MemberName.Equals(((MemberExpression)methodCalled.Body).Member.Name)
                                                             && operation.Instance == instance;
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
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
            Func<Operation, bool> predicate = (operation) => operation is MethodCall<T, U>
                                                             && operation.OperationState == OperationState.After
                                                             && operation.OperationMode == OperationMode.MethodCall
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.MemberName.Equals(((MemberExpression)methodCalled.Body).Member.Name);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
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
            Func<Operation, bool> predicate = (operation) => operation is MethodCall<T, U>
                                                             && operation.OperationState == OperationState.Before
                                                             && operation.OperationMode == OperationMode.MethodCall
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.MemberName.Equals(((MemberExpression)methodCalled.Body).Member.Name)
                                                             && operation.GlobalKey.Equals(key);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
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
            Func<Operation, bool> predicate = (operation) => operation is MethodCall<T, U>
                                                             && operation.OperationState == OperationState.After
                                                             && operation.OperationMode == OperationMode.MethodCall
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.MemberName.Equals(((MemberExpression)methodCalled.Body).Member.Name)
                                                             && instancePredicate((MethodCall<T, U>)operation);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
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
            Func<Operation, bool> predicate = (operation) => operation is MethodCall<T, U>
                                                             && operation.OperationState == OperationState.After
                                                             && operation.OperationMode == OperationMode.MethodCall
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.MemberName.Equals(((MemberExpression)methodCalled.Body).Member.Name);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
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
            Func<Operation, bool> predicate = (operation) => operation is MethodCall<T, U>
                                                             && operation.OperationState == OperationState.After
                                                             && operation.OperationMode == OperationMode.MethodCall
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.MemberName.Equals(((MemberExpression)methodCalled.Body).Member.Name)
                                                             && operation.GlobalKey.Equals(key);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
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
            Func<Operation, bool> predicate = (operation) => operation is MethodCall<T, U>
                                                             && operation.OperationState == OperationState.After
                                                             && operation.OperationMode == OperationMode.MethodCall
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.MemberName.Equals(((MemberExpression)methodCalled.Body).Member.Name)
                                                             && instancePredicate((MethodCall<T, U>)operation);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
        }

        /// <summary>
        /// Subscribes to a specific method call after it is called for the instance specified
        /// </summary>
        /// <param name="methodCalled"></param>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterCalled<U, A>(Expression<Func<T, Func<A, U>>> methodCalled, Expression<Action<MethodCall<T, U>>> subscriber, T instance)
        {
            Func<Operation, bool> predicate = (operation) => operation is MethodCall<T, U>
                                                             && operation.OperationState == OperationState.After
                                                             && operation.OperationMode == OperationMode.MethodCall
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.MemberName.Equals(((MemberExpression)methodCalled.Body).Member.Name)
                                                             && operation.Instance == instance;
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
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
            Func<Operation, bool> predicate = (operation) => operation is PropertyUpdate<T, U>
                                                             && operation.OperationState == OperationState.Before
                                                             && operation.OperationMode == OperationMode.PropertyChanged
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.MemberName.Equals(((MemberExpression)propertyChanged.Body).Member.Name);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
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
            Func<Operation, bool> predicate = (operation) => operation is PropertyUpdate<T, U>
                                                             && operation.OperationState == OperationState.Before
                                                             && operation.OperationMode == OperationMode.PropertyChanged
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.MemberName.Equals(((MemberExpression)propertyChanged.Body).Member.Name)
                                                             && operation.GlobalKey.Equals(key);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
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
            Func<Operation, bool> predicate = (operation) => operation is PropertyUpdate<T, U>
                                                             && operation.OperationState == OperationState.Before
                                                             && operation.OperationMode == OperationMode.PropertyChanged
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.MemberName.Equals(((MemberExpression)propertyChanged.Body).Member.Name)
                                                             && instancePredicate((PropertyUpdate<T, U>)operation);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
        }

        /// <summary>
        /// Subscribes to a property value change, before the change is applied for the instance specified
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <param name="propertyChanged"></param>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> BeforeChanged<U>(Expression<Func<T, U>> propertyChanged, Expression<Action<PropertyUpdate<T, U>>> subscriber, T instance)
        {
            Func<Operation, bool> predicate = (operation) => operation is PropertyUpdate<T, U>
                                                             && operation.OperationState == OperationState.Before
                                                             && operation.OperationMode == OperationMode.PropertyChanged
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.MemberName.Equals(((MemberExpression)propertyChanged.Body).Member.Name)
                                                             && operation.Instance == instance;
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
        }

        /// <summary>
        /// Subscribes to a property value change, after the change is applied
        /// </summary>
        /// <typeparam name="U">the property type</typeparam>
        /// <param name="method">the property to subscribe to</param>
        /// <param name="subscriber">the handle to call</param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterChanged<U>(Expression<Func<T, U>> propertyChanged, Expression<Action<PropertyUpdate<T, U>>> subscriber)
        {
            Func<Operation, bool> predicate = (operation) => operation is PropertyUpdate<T, U>
                                                             && operation.OperationState == OperationState.After
                                                             && operation.OperationMode == OperationMode.PropertyChanged
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.MemberName.Equals(((MemberExpression)propertyChanged.Body).Member.Name);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
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
            Func<Operation, bool> predicate = (operation) => operation is PropertyUpdate<T, U>
                                                             && operation.OperationState == OperationState.After
                                                             && operation.OperationMode == OperationMode.PropertyChanged
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.MemberName.Equals(((MemberExpression)propertyChanged.Body).Member.Name)
                                                             && operation.GlobalKey.Equals(key);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
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
            Func<Operation, bool> predicate = (operation) => operation is PropertyUpdate<T, U>
                                                             && operation.OperationState == OperationState.After
                                                             && operation.OperationMode == OperationMode.PropertyChanged
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.MemberName.Equals(((MemberExpression)propertyChanged.Body).Member.Name)
                                                             && instancePredicate((PropertyUpdate<T, U>)operation);
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
        }

        /// <summary>
        /// Subscribes to a property value change, after the change is applied for the instance specified
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <param name="propertyChanged"></param>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public SubscriptionConfig<T> AfterChanged<U>(Expression<Func<T, U>> propertyChanged, Expression<Action<PropertyUpdate<T, U>>> subscriber, T instance)
        {
            Func<Operation, bool> predicate = (operation) => operation is PropertyUpdate<T, U>
                                                             && operation.OperationState == OperationState.After
                                                             && operation.OperationMode == OperationMode.PropertyChanged
                                                             && operation.InstanceType.Equals(typeof(T))
                                                             && operation.MemberName.Equals(((MemberExpression)propertyChanged.Body).Member.Name)
                                                             && operation.Instance == instance;
            var action = subscriber.Compile();
            var selector = new SubscriptionSelector(predicate, action);
            AddSelector(selector);
            return this;
        }

        /// <summary>
        /// Creates the subscription and starts listening for operations.  Disposing of the returned Subscription instance releases the subscription.
        /// </summary>
        /// <returns></returns>
        public Subscription<T> Subscribe()
        {
            var subscription = new Subscription<T>(this);
            App.Resolve<IManageSubscriptions>().Add(subscription);
            return subscription;
        }
    }
}
