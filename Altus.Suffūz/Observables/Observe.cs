using Altus.Suffūz.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Altus.Suffūz.Collections.Linq;
using Altus.Suffūz.Routing;
using Altus.Suffūz.Collections;

namespace Altus.Suffūz.Observables
{
    public class Observe
    {
        static ExclusiveLock _syncRoot = new ExclusiveLock("Observe");

        static Observe()
        {
            Observables = new Dictionary<string, object>();
            Providers = new List<ChannelProviderRegistration>();
            Vectors = new Dictionary<string, IDictionary<ushort, VersionVectorInstance>>();
            Subscriptions = new Dictionary<string, Subscription>();
            Hasher = MD5.Create();
        }

        protected static IDictionary<string, IDictionary<ushort, VersionVectorInstance>> Vectors { get; private set; }
        protected static IDictionary<string, object> Observables { get; private set; }
        protected static IList<ChannelProviderRegistration> Providers { get; private set; }
        protected static IDictionary<string, Subscription> Subscriptions { get; private set; }

        protected static MD5 Hasher { get; private set; }

        public static string NewKey()
        {
            return Convert.ToBase64String(Hasher.ComputeHash(Guid.NewGuid().ToByteArray())).Substring(0, 22);
        }

        protected static T CreateObservable<T>(T instance, string globalKey) where T : class, new()
        {
            var builder = App.Resolve<IObservableBuilder>();
            var wrappedInstance = builder.Create<T>(instance, globalKey, App.Resolve<IPublisher>());

            // subscribe to changes, so we can increment our local version numbers
            Subscriptions[globalKey] = Observe<T>.AfterAny((e) => AfterAny(e)).Subscribe();

            return wrappedInstance;
        }

        private static void AfterAny<T>(AnyOperation<T> e) where T : class, new()
        {
            _syncRoot.Lock(() =>
            {
                var vector = Vectors[e.GlobalKey][App.InstanceId];
                switch(e.OperationMode)
                {
                    case OperationMode.Created:
                        {
                            // nothing to do
                            break;
                        }
                    case OperationMode.Disposed:
                        {
                            // nothing to do
                            break;
                        }
                    case OperationMode.PropertyCall:
                        {
                            // update property vector and instance vector
                            vector.Version++;
                            var memberVector = vector.MemberVectors.SingleOrDefault(vve => vve.Key == e.MemberName);
                            if (memberVector == null)
                            {
                                memberVector = new VersionVectorEntry<object>()
                                {
                                    IdentityId = App.InstanceId,
                                    Key = e.MemberName,
                                    Value = e.Value,
                                    Version = 1
                                };
                                vector.MemberVectors.Add(memberVector);
                            }
                            else
                            {
                                memberVector.Value = e.Value;
                                memberVector.Version++;
                            }

                            // save it
                            Vectors[e.GlobalKey][App.InstanceId] = vector;
                            break;
                        }
                    case OperationMode.MethodCall:
                        {
                            // nothing to do
                            break;
                        }
                }
            });
        }

        /// <summary>
        /// Gets an observable instance of T, using the default constructor, assigning the provided globalObjectKey identifier to the instance
        /// </summary>
        /// <param name="globalObjectKey">a gloablly unique Id used to identify the instance</param>
        /// <returns></returns>
        public static T Get<T>(string globalObjectKey) where T : class, new()
        {
            return Get(() => new T(), globalObjectKey);
        }

        /// <summary>
        /// Gets an observable instance of T, using the default constructor, and assigning a newly generated globally unique identifier to the instance
        /// </summary>
        /// <returns></returns>
        public static T Get<T>() where T : class, new()
        {
            return Get<T>(NewKey());
        }

        /// <summary>
        /// Gets (or creates, if this is the first call) an observable instance of type T, using the provided default instance creator and 
        /// assigning a newly generated globally unique identifier.
        /// </summary>
        /// <param name="creator"></param>
        /// <returns></returns>
        public static T Get<T>(Func<T> creator) where T : class, new()
        {
            return Get(creator, NewKey());
        }

        /// <summary>
        /// Gets (or creates, if this is the first call) an observable instance of type T, using the provided default instance creator and 
        /// global key specified.
        /// </summary>
        /// <param name="creator"></param>
        /// <param name="globalKey"></param>
        /// <returns></returns>
        public static T Get<T>(Func<T> creator, string globalKey) where T : class, new()
        {
            object instance;
            _syncRoot.Enter();
            try
            {
                if (!Observables.TryGetValue(globalKey, out instance))
                {
                    var beforeCreated = new Created<T>(globalKey, OperationState.Before, null);
                    ObservableResponse defaultResponse = new ObservableResponse()
                    {
                        GlobalKey = globalKey,
                        Vector = new VersionVectorInstance<T>()
                        {
                            IdentityId = App.InstanceId,
                            Key = "Instance",
                            Value = creator(),
                            Version = 0,
                        }
                    };

                    // because we've never seen this before, we're going to need to register routes to 
                    // handle status class and state change messages for this instance
                    var router = App.Resolve<IServiceRouter>();

                    // get the instance from the network, if it exists, otherwise, build a new one
                    foreach (var provider in Providers)
                    {
                        foreach (var channel in provider.Provider.GetChannels(beforeCreated))
                        {
                            // register the local route handler so we can answer calls requesting our latest version
                            router.Route<ObserveHandler, ObservableRequest, ObservableResponse>(channel.Name, (handler, request) => handler.GetCurrent(request));

                            var vector = Post<ObservableRequest, ObservableResponse>
                                            .Via(channel.Name, new ObservableRequest() { GlobalKey = globalKey })
                                            .Execute();

                            if (vector != null && vector.Vector.Version > defaultResponse.Vector.Version)
                            {
                                defaultResponse.Vector.Value = vector.Vector.Value;
                                defaultResponse.Vector.Version = vector.Vector.Version;
                                defaultResponse.Vector.MemberVectors = vector.Vector.MemberVectors;
                            }
                        }
                    }

                    Observables.Add(globalKey, instance);
                    IDictionary<ushort, VersionVectorInstance> vectors;
                    if (!Vectors.TryGetValue(globalKey, out vectors))
                    {
                        vectors = CreateVectorDictionary();
                        Vectors.Add(globalKey, vectors);
                    }


                    // capture my version vector for this key
                    vectors[App.InstanceId] = defaultResponse.Vector;
                    instance = CreateObservable<T>((T)defaultResponse.Vector.Value, globalKey); // wrap it in a local observable
                }
            
                return (T)instance;
            }
            finally
            {
                _syncRoot.Exit();
            }
        }

        private static IDictionary<ushort, VersionVectorInstance> CreateVectorDictionary()
        {
            var manager = App.Resolve<IManagePersistentCollections>();
            return manager.GetOrCreate<IPersistentDictionary<ushort, VersionVectorInstance>>(
                "observable_vectors.bin", 
                (file) => new PersistentDictionary<ushort, VersionVectorInstance>(file, manager.GlobalHeap, false));
        }

        /// <summary>
        /// Subscribes to instance disposal event, before the instance is disposed for the provided instance
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static Subscription<T> BeforeDisposed<T>(Expression<Action<Disposed<T>>> subscriber, T instance) where T : class, new()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to instance disposal event, after the instance is disposed for the provided instance
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static Subscription<T> AfterDisposed<T>(Expression<Action<Disposed<T>>> subscriber, T instance) where T : class, new()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to any property update or method call, before they are applied for the instance specified
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static Subscription<T> BeforeAny<T>(Expression<Action<AnyOperation<T>>> subscriber, T instance) where T : class, new()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to any property update or method call, after they are applied for the instance specified
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static Subscription<T> AfterAny<T>(Expression<Action<AnyOperation<T>>> subscriber, T instance) where T : class, new()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a specific method call before it is called for the instance specified
        /// </summary>
        /// <param name="methodCalled"></param>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static Subscription<T> BeforeCalled<T, U>(Expression<Action<T>> methodCalled, Expression<Action<MethodCall<T, U>>> subscriber, T instance) where T : class, new()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a specific method call after it is called for the instance specified
        /// </summary>
        /// <param name="methodCalled"></param>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static Subscription<T> AfterCalled<T, U>(Expression<Action<T>> methodCalled, Expression<Action<MethodCall<T, U>>> subscriber, T instance) where T : class, new()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a property value change, before the change is applied for the instance specified
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <param name="propertyChanged"></param>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static Subscription<T> BeforeChanged<T, U>(Expression<Func<T, U>> propertyChanged, Expression<Action<PropertyUpdate<T, U>>> subscriber, T instance) where T : class, new()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a property value change, after the change is applied for the instance specified
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <param name="propertyChanged"></param>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static Subscription<T> AfterChanged<T, U>(Expression<Func<T, U>> propertyChanged, Expression<Action<PropertyUpdate<T, U>>> subscriber, T instance) where T : class, new()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Registers a channel provider with the Observable framework, returning the registration.  The platform passes observable event messages to each 
        /// channel provider to route messages onto to relevant IChannels for message distribution.
        /// </summary>
        /// <param name="cp"></param>
        /// <returns></returns>
        public static ChannelProviderRegistration RegisterChannelProvider(IObservableChannelProvider cp)
        {
            return _syncRoot.Lock(() =>
            {
                var provider = new ChannelProviderRegistration(cp);
                Providers.Add(provider);
                return provider;
            });
        }

        public class ObserveHandler
        {
            public ObserveHandler() { }

            internal ObservableResponse GetCurrent(ObservableRequest request)
            {
                VersionVectorInstance entry;
                ObservableResponse response = new ObservableResponse()
                {
                    GlobalKey = request.GlobalKey,
                    Vector = new VersionVectorInstance()
                    {
                        IdentityId = App.InstanceId,
                        Key = "Instance",
                        Value = null,
                        Version = 0
                    }
                };

                IDictionary<ushort, VersionVectorInstance> vectors;
                if (Observe.Vectors.TryGetValue(request.GlobalKey, out vectors)
                    && vectors.TryGetValue(App.InstanceId, out entry))
                {
                    // strip out the observable wrapping from the instance before sending it
                    response.Vector.Value = ((IObservable)entry.Value).Instance;
                    response.Vector.Version = entry.Version;
                    response.Vector.MemberVectors = entry.MemberVectors;
                }
                return response;
            }
        }
    }

    public class Observe<T> : Observe where T : class, new()
    {
        /// <summary>
        /// Subscribes to instance creation event, before the instance is created.  This will only be called once per globally unique id for type T, 
        /// until that instance is disposed by all subscribers.
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public static SubscriptionConfig<T> BeforeCreated(Expression<Action<Created<T>>> subscriber)
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
        public static SubscriptionConfig<T> BeforeCreated(Expression<Action<Created<T>>> subscriber, string key)
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
        public static SubscriptionConfig<T> BeforeCreated(Expression<Action<Created<T>>> subscriber, Func<Created<T>, bool> instanceSelector)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to instance creation event, after the instance is created
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public static SubscriptionConfig<T> AfterCreated(Expression<Action<Created<T>>> subscriber)
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
        public static SubscriptionConfig<T> AfterCreated(Expression<Action<Created<T>>> subscriber, string key)
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
        public static SubscriptionConfig<T> AfterCreated(Expression<Action<Created<T>>> subscriber, Func<Created<T>, bool> instanceSelector)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to instance disposal event, before the instance is disposed
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public static SubscriptionConfig<T> BeforeDisposed(Expression<Action<Disposed<T>>> subscriber)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to instance disposal event, before the instance is disposed, for the specified instance key.
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static SubscriptionConfig<T> BeforeDisposed(Expression<Action<Disposed<T>>> subscriber, string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to instance disposal event, before the instance is disposed, for the instances matching the instanceSelector
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instanceSelector"></param>
        /// <returns></returns>
        public static SubscriptionConfig<T> BeforeDisposed(Expression<Action<Disposed<T>>> subscriber, Func<Disposed<T>, bool> instanceSelector)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to instance disposal event, before the instance is disposed for the provided instance
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static SubscriptionConfig<T> BeforeDisposed(Expression<Action<Disposed<T>>> subscriber, T instance)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to instance disposal event, after the instance is disposed
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public static SubscriptionConfig<T> AfterDisposed(Expression<Action<Disposed<T>>> subscriber)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to instance disposal event, after the instance is disposed, for the specified instance key.
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static SubscriptionConfig<T> AfterDisposed(Expression<Action<Disposed<T>>> subscriber, string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to instance disposal event, after the instance is disposed, for the instances matching the instanceSelector
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instanceSelector"></param>
        /// <returns></returns>
        public static SubscriptionConfig<T> AfterDisposed(Expression<Action<Disposed<T>>> subscriber, Func<Disposed<T>, bool> instanceSelector)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to instance disposal event, after the instance is disposed for the provided instance
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static SubscriptionConfig<T> AfterDisposed(Expression<Action<Disposed<T>>> subscriber, T instance)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to any property update or method call, before they are applied
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public static SubscriptionConfig<T> BeforeAny(Expression<Action<AnyOperation<T>>> subscriber)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to any property update or method call, before they are applied for the instance specified by key
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static SubscriptionConfig<T> BeforeAny(Expression<Action<AnyOperation<T>>> subscriber, string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to any property update or method call, before they are applied for the instances matched by instanceSelector predicate
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instanceSelector"></param>
        /// <returns></returns>
        public static SubscriptionConfig<T> BeforeAny(Expression<Action<AnyOperation<T>>> subscriber, Func<AnyOperation<T>, bool> instanceSelector)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to any property update or method call, before they are applied for the instance specified
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static SubscriptionConfig<T> BeforeAny(Expression<Action<AnyOperation<T>>> subscriber, T instance)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to any property update or method call, after they are applied
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public static SubscriptionConfig<T> AfterAny(Expression<Action<AnyOperation<T>>> subscriber)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to any property update or method call, after they are applied for the instance specified by key
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static SubscriptionConfig<T> AfterAny(Expression<Action<AnyOperation<T>>> subscriber, string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to any property update or method call, after they are applied for the instances matched by instanceSelector predicate
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instanceSelector"></param>
        /// <returns></returns>
        public static SubscriptionConfig<T> AfterAny(Expression<Action<AnyOperation<T>>> subscriber, Func<AnyOperation<T>, bool> instanceSelector)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to any property update or method call, after they are applied for the instance specified
        /// </summary>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static SubscriptionConfig<T> AfterAny(Expression<Action<AnyOperation<T>>> subscriber, T instance)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///  Subscribes to a specific method call before it is called
        /// </summary>
        /// <param name="methodCalled">the method to subscribe to</param>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public static SubscriptionConfig<T> BeforeCalled<U>(Expression<Action<T>> methodCalled, Expression<Action<MethodCall<T, U>>> subscriber)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a specific method call before it is called for the instance specified by key
        /// </summary>
        /// <param name="methodCalled"></param>
        /// <param name="subscriber"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static SubscriptionConfig<T> BeforeCalled<U>(Expression<Action<T>> methodCalled, Expression<Action<MethodCall<T, U>>> subscriber, string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a specific method call before it is called for the instances specified by instancePredicate
        /// </summary>
        /// <param name="methodCalled"></param>
        /// <param name="subscriber"></param>
        /// <param name="instancePredicate"></param>
        /// <returns></returns>
        public static SubscriptionConfig<T> BeforeCalled<U>(Expression<Action<T>> methodCalled, Expression<Action<MethodCall<T, U>>> subscriber, Func<MethodCall<T, U>, bool> instancePredicate)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a specific method call before it is called for the instance specified
        /// </summary>
        /// <param name="methodCalled"></param>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static SubscriptionConfig<T> BeforeCalled<U>(Expression<Action<T>> methodCalled, Expression<Action<MethodCall<T, U>>> subscriber, T instance)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a specific method call after it is called
        /// </summary>
        /// <param name="method">the method to subscribe to</param>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public static SubscriptionConfig<T> AfterCalled<U>(Expression<Action<T>> method, Expression<Action<MethodCall<T, U>>> subscriber)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Subscribes to a specific method call after it is called for the instance specified by key
        /// </summary>
        /// <param name="methodCalled"></param>
        /// <param name="subscriber"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static SubscriptionConfig<T> AfterCalled<U>(Expression<Action<T>> methodCalled, Expression<Action<MethodCall<T, U>>> subscriber, string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a specific method call after it is called for the instances specified by instancePredicate
        /// </summary>
        /// <param name="methodCalled"></param>
        /// <param name="subscriber"></param>
        /// <param name="instancePredicate"></param>
        /// <returns></returns>
        public static SubscriptionConfig<T> AfterCalled<U>(Expression<Action<T>> methodCalled, Expression<Action<MethodCall<T, U>>> subscriber, Func<MethodCall<T, U>, bool> instancePredicate)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a specific method call after it is called for the instance specified
        /// </summary>
        /// <param name="methodCalled"></param>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static SubscriptionConfig<T> AfterCalled<U>(Expression<Action<T>> methodCalled, Expression<Action<MethodCall<T, U>>> subscriber, T instance)
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
        public static SubscriptionConfig<T> BeforeChanged<U>(Expression<Func<T, U>> propertyChanged, Expression<Action<PropertyUpdate<T, U>>> subscriber)
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
        public static SubscriptionConfig<T> BeforeChanged<U>(Expression<Func<T, U>> propertyChanged, Expression<Action<PropertyUpdate<T, U>>> subscriber, string key)
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
        public static SubscriptionConfig<T> BeforeChanged<U>(Expression<Func<T, U>> propertyChanged, Expression<Action<PropertyUpdate<T, U>>> subscriber, Func<PropertyUpdate<T, U>, bool> instancePredicate)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a property value change, before the change is applied for the instance specified
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <param name="propertyChanged"></param>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static SubscriptionConfig<T> BeforeChanged<U>(Expression<Func<T, U>> propertyChanged, Expression<Action<PropertyUpdate<T, U>>> subscriber, T instance)
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
        public static SubscriptionConfig<T> AfterChanged<U>(Expression<Func<T, U>> method, Expression<Action<PropertyUpdate<T, U>>> subscriber)
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
        public static SubscriptionConfig<T> AfterChanged<U>(Expression<Func<T, U>> propertyChanged, Expression<Action<PropertyUpdate<T, U>>> subscriber, string key)
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
        public static SubscriptionConfig<T> AfterChanged<U>(Expression<Func<T, U>> propertyChanged, Expression<Action<PropertyUpdate<T, U>>> subscriber, Func<PropertyUpdate<T, U>, bool> instancePredicate)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribes to a property value change, after the change is applied for the instance specified
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <param name="propertyChanged"></param>
        /// <param name="subscriber"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static SubscriptionConfig<T> AfterChanged<U>(Expression<Func<T, U>> propertyChanged, Expression<Action<PropertyUpdate<T, U>>> subscriber, T instance)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates the subscription and starts listening for operations.  Disposing of the returned Subscription instance releases the subscription.
        /// </summary>
        /// <returns></returns>
        public static SubscriptionConfig<T> Subscribe()
        {
            throw new NotImplementedException();
        }
    }
    
}
