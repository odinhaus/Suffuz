using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Altus.Suffūz.Observables.Tests.Observables;
using Altus.Suffūz.Observables;
using Altus.Suffūz.Protocols;
using System.Collections.Generic;
using System.Net;
using Altus.Suffūz.Threading;
using Altus.Suffūz.Serialization;
using Altus.Suffūz.DependencyInjection;
using Altus.Suffūz.Collections;
using Altus.Suffūz.Serialization.Binary;
using Altus.Suffūz.Protocols.Udp;
using Altus.Suffūz.Scheduling;

namespace Altus.Suffūz.Objects.Tests
{
    [TestClass]
    public class MainTests : IBootstrapper
    {
        static IPEndPoint _nextEndPoint = new IPEndPoint(IPAddress.Parse("230.0.1.0"), 5000);
        // simple construct for sharing and executing exclusive locks across type instances
        static ExclusiveLock SyncLock = new ExclusiveLock("observableChannelLock");

        public string InstanceName
        {
            get { return "Server1"; }
        }

        public ushort InstanceId
        {
            get { return 1; }
        }

        public byte[] InstanceCryptoKey
        {
            get { return new byte[16]; }
        }

        [TestMethod]
        public void CanGetObjectInstance()
        {
            #region Infrastructure Setup
            // setup the synchronization channel services
            IObservableChannelProvider 
                cp1 = new BestEffortObservableChannelProvider(
                 (op) =>
                 {
                     // we lock this section to prevent simultaneous creation of new channels
                     return SyncLock.Lock(() =>
                     {
                         // channel per type strategy - will create a separate channel for each state object type being synchronized
                         // the default strategy puts all synchronization messages onto a single channel
                         if (!BestEffortObservableChannelProvider.DefaultChannelService.CanCreate(op.InstanceType.FullName))
                         {
                             BestEffortObservableChannelProvider.DefaultChannelService
                             .Register(op.InstanceType.FullName, _nextEndPoint.Increment(maxIP: "249.0.0.0"));
                         }
                         // return the channel enumeration (only one channel will be returned per type name)
                         var channels = new List<IChannel>();
                         channels.Add(BestEffortObservableChannelProvider.DefaultChannelService.Create(op.InstanceType.FullName));
                         return channels;
                     });
                  }
                ), 
                cp2 = new SignalRObservableChannelProvider(),
                cp3 = new iOSObservableChannelProvider();

            // setup the infrastructure to replicate event messages across your system
            // this example would provide synchronization across windows servers over Multicast, javascript (and other) clients 
            // over SignalR, and iOS devices using APNS
            var registration1 = Observe.RegisterChannelProvider(cp1); // register for best-effort multicast event synchronization
            var registration2 = Observe.RegisterChannelProvider(cp2); // register for SignalR javascript client notifications
            var registration3 = Observe.RegisterChannelProvider(cp3); // register for iOS push notifications
            #endregion

            #region Global Subscription
            // create event subscriptions using any of the three overloads for assigning to events for all instances of a type,
            // a specific instance as identified by its global key, or by providing a selection predicate to determine which 
            // instances to receive events for
            var subscription1 = Observe<StateClass> .BeforeCreated((e) => this.BeforeCreated(e))
                                                    .BeforeCreated((e) => this.BeforeCreated(e), "some key")
                                                    .BeforeCreated((e) => this.BeforeCreated(e), (e) => e.GlobalKey == "some key")

                                                    .AfterCreated((e) => this.AfterCreated(e))
                                                    .AfterCreated((e) => this.AfterCreated(e), "some key")
                                                    .AfterCreated((e) => this.AfterCreated(e), (e) => e.GlobalKey == "some key")

                                                    .BeforeDisposed((e) => this.BeforeDisposed(e))
                                                    .BeforeDisposed((e) => this.BeforeDisposed(e), "some key")
                                                    .BeforeDisposed((e) => this.BeforeDisposed(e), (e) => e.GlobalKey == "some key")

                                                    .AfterDisposed((e) => this.AfterDisposed(e))
                                                    .AfterDisposed((e) => this.AfterDisposed(e), "some key")
                                                    .AfterDisposed((e) => this.AfterDisposed(e), (e) => e.GlobalKey == "some key")

                                                    .BeforeAny((e) => this.BeforeAny(e))
                                                    .BeforeAny((e) => this.BeforeAny(e), "some key")
                                                    .BeforeAny((e) => this.BeforeAny(e), (e) => e.GlobalKey == "some key")

                                                    .AfterAny((e) => this.AfterAny(e))
                                                    .AfterAny((e) => this.AfterAny(e), "some key")
                                                    .AfterAny((e) => this.AfterAny(e), (e) => e.GlobalKey == "some key")

                                                    .BeforeCalled<int, string>((s) => s.Hello, (e) => this.BeforeHello(e))
                                                    .BeforeCalled<int, string>((s) => s.Hello, (e) => this.BeforeHello(e), "some key")
                                                    .BeforeCalled<int, string>((s) => s.Hello, (e) => this.BeforeHello(e), (e) => e.GlobalKey == "some key")

                                                    .AfterCalled<int, string>((s) => s.Hello, (e) => this.AfterHello(e))
                                                    .AfterCalled<int, string>((s) => s.Hello, (e) => this.AfterHello(e), "some key")
                                                    .AfterCalled<int, string>((s) => s.Hello, (e) => this.AfterHello(e), (e) => e.GlobalKey == "some key")

                                                    .BeforeChanged((s) => s.Size, (e) => this.BeforeSizeChanged(e))
                                                    .BeforeChanged((s) => s.Size, (e) => this.BeforeSizeChanged(e), "some key")
                                                    .BeforeChanged((s) => s.Size, (e) => this.BeforeSizeChanged(e), (e) => e.GlobalKey == "some key")

                                                    .AfterChanged((s) => s.Size, (e) => this.AfterSizeChanged(e))
                                                    .AfterChanged((s) => s.Size, (e) => this.AfterSizeChanged(e), "some key")
                                                    .AfterChanged((s) => s.Size, (e) => this.AfterSizeChanged(e), (e) => e.GlobalKey == "some key")

                                                    .Subscribe();
            #endregion

            

            #region Instance Usage and Subscription
            // get (or create) an observable instance of type StateClass with a global key of "some key"
            var observable = Observe.Get(() => new StateClass { Size = 2, Score = 3 }, "some key");
            // subscribe to this instance
            var subscription2 = Observe.BeforeAny((e) => this.BeforeAny(e), observable);
            // update a value, triggering a change event to be replicated across the distribution channels configured previously
            // calling all subscribers on each channel for this shared instance
            observable.Size = 4;
            #endregion
        }

        [TestMethod]
        public void CanCreateNewKeys()
        {
            var key = Observe.NewKey();
            Assert.IsTrue(key.Length == 22);
        }

        [TestMethod]
        public void CanCreateObservableProxy()
        {
            var builder = new ILObservableTypeBuilder();
            var proxyType = builder.Build(typeof(StateClass));

            Assert.IsTrue(proxyType.BaseType == typeof(StateClass));
            Assert.IsTrue(proxyType.Implements<Observables.IObservable<StateClass>>());
            Assert.IsTrue(proxyType.GetConstructor(new Type[] { typeof(IPublisher), typeof(StateClass), typeof(string) }) != null);

            var baseInstance = new StateClass();
            var publisher = new FakePublisher();
            var key = "a key";
            var instance = Activator.CreateInstance(proxyType, new object[] { publisher, baseInstance, key}) as StateClass;

            Assert.IsTrue(((Observables.IObservable<StateClass>)instance).GlobalKey == key);
            Assert.IsTrue(((Observables.IObservable<StateClass>)instance).Instance == baseInstance);
            Assert.IsTrue(((Observables.IObservable<StateClass>)instance).SyncLock != null);

            instance.Size = 5;

            Assert.IsTrue(publisher.LastPropertyUpdate.EventClass == EventClass.Commutative);
            Assert.IsTrue(publisher.LastPropertyUpdate.EventOrder == EventOrder.Additive);
            Assert.IsTrue(publisher.LastPropertyUpdate.MemberName == "Size");
            Assert.IsTrue(publisher.LastPropertyUpdate.OperationMode == OperationMode.PropertyCall);
            Assert.IsTrue(publisher.LastPropertyUpdate.OperationState == OperationState.After);
            Assert.IsTrue(((PropertyUpdate<StateClass, int>)publisher.LastPropertyUpdate).BaseValue == 0);
            Assert.IsTrue(((PropertyUpdate<StateClass, int>)publisher.LastPropertyUpdate).NewValue == 5);

            instance.Age = 10;
            instance.Score = 12;

            instance.Name = "Foo";
            Assert.IsTrue(publisher.LastPropertyUpdate.EventClass == EventClass.Explicit);
            Assert.IsTrue(publisher.LastPropertyUpdate.EventOrder == EventOrder.Logical);
            Assert.IsTrue(publisher.LastPropertyUpdate.MemberName == "Name");
            Assert.IsTrue(publisher.LastPropertyUpdate.OperationMode == OperationMode.PropertyCall);
            Assert.IsTrue(publisher.LastPropertyUpdate.OperationState == OperationState.After);
            Assert.IsTrue(((PropertyUpdate<StateClass, string>)publisher.LastPropertyUpdate).BaseValue == null);
            Assert.IsTrue(((PropertyUpdate<StateClass, string>)publisher.LastPropertyUpdate).NewValue == "Foo");

            Assert.IsTrue(instance.Size == 5);
            Assert.IsTrue(instance.Age == 10);
            Assert.IsTrue(instance.Score == 12);
        }

        [TestMethod]
        public void CanGetLocalObservableInstance()
        {
            // because we didn't register any observable channel providers, this will just create a local copy
            var instance = Observe.Get<StateClass>("some key");
            Assert.IsTrue(instance.GetType().BaseType == typeof(StateClass));
            Assert.IsTrue(instance.GetType().Implements<Observables.IObservable<StateClass>>());
            Assert.IsTrue(instance.GetType().GetConstructor(new Type[] { typeof(IPublisher), typeof(StateClass), typeof(string) }) != null);
            Assert.IsTrue(((Observables.IObservable<StateClass>)instance).GlobalKey == "some key");
            Assert.IsTrue(((Observables.IObservable<StateClass>)instance).Instance is StateClass);
            Assert.IsTrue(((Observables.IObservable<StateClass>)instance).SyncLock != null);
            var publisher = ((Observables.IObservable<StateClass>)instance).Publisher as FakePublisher;
            instance.Age = 5;
            Assert.IsTrue(publisher.LastPropertyUpdate.EventClass == EventClass.Commutative);
            Assert.IsTrue(publisher.LastPropertyUpdate.EventOrder == EventOrder.Additive);
            Assert.IsTrue(publisher.LastPropertyUpdate.MemberName == "Age");
            Assert.IsTrue(publisher.LastPropertyUpdate.OperationMode == OperationMode.PropertyCall);
            Assert.IsTrue(publisher.LastPropertyUpdate.OperationState == OperationState.After);
            Assert.IsTrue(((PropertyUpdate<StateClass, int>)publisher.LastPropertyUpdate).BaseValue == 0);
            Assert.IsTrue(((PropertyUpdate<StateClass, int>)publisher.LastPropertyUpdate).NewValue == 5);
            var timestamp = ((PropertyUpdate<StateClass, int>)publisher.LastPropertyUpdate).LocalTimestamp;
            instance.Age = 5; // should NOT trigger a new publication!
            Assert.IsTrue(((PropertyUpdate<StateClass, int>)publisher.LastPropertyUpdate).BaseValue == 0);
            Assert.IsTrue(((PropertyUpdate<StateClass, int>)publisher.LastPropertyUpdate).NewValue == 5);
            Assert.IsTrue(((PropertyUpdate<StateClass, int>)publisher.LastPropertyUpdate).LocalTimestamp == timestamp); // no change in timestamp
            instance.Name = "Foo";
            Assert.IsTrue(publisher.LastPropertyUpdate.EventClass == EventClass.Explicit);
            Assert.IsTrue(publisher.LastPropertyUpdate.EventOrder == EventOrder.Logical);
            Assert.IsTrue(publisher.LastPropertyUpdate.MemberName == "Name");
            Assert.IsTrue(publisher.LastPropertyUpdate.OperationMode == OperationMode.PropertyCall);
            Assert.IsTrue(publisher.LastPropertyUpdate.OperationState == OperationState.After);
            Assert.IsTrue(((PropertyUpdate<StateClass, string>)publisher.LastPropertyUpdate).BaseValue == null);
            Assert.IsTrue(((PropertyUpdate<StateClass, string>)publisher.LastPropertyUpdate).NewValue == "Foo");
        }

        [TestMethod]
        public void CanGetSuffuzObjectInstance()
        {
            // setup the infrastructure to replicate event messages across your system
            // this example would provide synchronization across windows servers over Multicast, javascript (and other) clients 
            // over SignalR, and iOS devices using APNS
            foreach (var provider in App.ResolveAll<IObservableChannelProvider>())
            {
                Observe.RegisterChannelProvider(provider); // register provider
            }

            // this will attempt to locate the latest instance from all participants on the registered channels
            var instance = Observe.Get<StateClass>("some key");
            Assert.IsTrue(instance.GetType().BaseType == typeof(StateClass));
            Assert.IsTrue(instance.GetType().Implements<Observables.IObservable<StateClass>>());
            Assert.IsTrue(instance.GetType().GetConstructor(new Type[] { typeof(IPublisher), typeof(StateClass), typeof(string) }) != null);
            Assert.IsTrue(((Observables.IObservable<StateClass>)instance).GlobalKey == "some key");
            Assert.IsTrue(((Observables.IObservable<StateClass>)instance).Instance is StateClass);
            Assert.IsTrue(((Observables.IObservable<StateClass>)instance).SyncLock != null);
            var publisher = ((Observables.IObservable<StateClass>)instance).Publisher as FakePublisher;
            instance.Age = 5;
            Assert.IsTrue(publisher.LastPropertyUpdate.EventClass == EventClass.Commutative);
            Assert.IsTrue(publisher.LastPropertyUpdate.EventOrder == EventOrder.Additive);
            Assert.IsTrue(publisher.LastPropertyUpdate.MemberName == "Age");
            Assert.IsTrue(publisher.LastPropertyUpdate.OperationMode == OperationMode.PropertyCall);
            Assert.IsTrue(publisher.LastPropertyUpdate.OperationState == OperationState.After);
            Assert.IsTrue(((PropertyUpdate<StateClass, int>)publisher.LastPropertyUpdate).BaseValue == 0);
            Assert.IsTrue(((PropertyUpdate<StateClass, int>)publisher.LastPropertyUpdate).NewValue == 5);
            var timestamp = ((PropertyUpdate<StateClass, int>)publisher.LastPropertyUpdate).LocalTimestamp;
            instance.Age = 5; // should NOT trigger a new publication!
            Assert.IsTrue(((PropertyUpdate<StateClass, int>)publisher.LastPropertyUpdate).BaseValue == 0);
            Assert.IsTrue(((PropertyUpdate<StateClass, int>)publisher.LastPropertyUpdate).NewValue == 5);
            Assert.IsTrue(((PropertyUpdate<StateClass, int>)publisher.LastPropertyUpdate).LocalTimestamp == timestamp); // no change in timestamp
            instance.Name = "Foo";
            Assert.IsTrue(publisher.LastPropertyUpdate.EventClass == EventClass.Explicit);
            Assert.IsTrue(publisher.LastPropertyUpdate.EventOrder == EventOrder.Logical);
            Assert.IsTrue(publisher.LastPropertyUpdate.MemberName == "Name");
            Assert.IsTrue(publisher.LastPropertyUpdate.OperationMode == OperationMode.PropertyCall);
            Assert.IsTrue(publisher.LastPropertyUpdate.OperationState == OperationState.After);
            Assert.IsTrue(((PropertyUpdate<StateClass, string>)publisher.LastPropertyUpdate).BaseValue == null);
            Assert.IsTrue(((PropertyUpdate<StateClass, string>)publisher.LastPropertyUpdate).NewValue == "Foo");

            var anotherInstance = Observe.Get<StateClass>("some key");
            Assert.IsTrue(anotherInstance.Name == instance.Name);
        }

        [TestMethod]
        public void CanBinarySerializeChangeState()
        {
            var changeState = new ChangeState<int>()
            {
                Epoch = 1,
                PropertyName = "Foo",
                ObservableId = "Fum"
            };

            changeState.Add(new Change<int>() { BaseValue = 3, NewValue = 5, Timestamp = CurrentTime.Now });

            var serializer = App.Resolve<ISerializationContext>().GetSerializer<ChangeState>(StandardFormats.BINARY);
            var bytes = serializer.Serialize(changeState);
            var d = serializer.Deserialize(bytes);

            Assert.IsTrue(d.Epoch == changeState.Epoch);
            Assert.IsTrue(d.PropertyName == changeState.PropertyName);
            Assert.IsTrue(d.ObservableId == changeState.ObservableId);
            Assert.IsTrue(d.Count == changeState.Count);
            Assert.IsTrue(d[0].Equals(changeState[0]));
        }

        [TestMethod]
        public void CanJSONSerializeChangeState()
        {
            var changeState = new ChangeState<int>()
            {
                Epoch = 1,
                PropertyName = "Foo",
                ObservableId = "Fum"
            };

            changeState.Add(new Change<int>() { BaseValue = 3, NewValue = 5, Timestamp = CurrentTime.Now });

            var serializationContext = App.Resolve<ISerializationContext>();
            serializationContext.SetSerializer(typeof(ChangeState), typeof(Altus.Suffūz.Observables.Serialization.JS.ChangeStateSerializer), StandardFormats.JSON);

            var serializer = serializationContext.GetSerializer<ChangeState>(StandardFormats.JSON);
            var bytes = serializer.Serialize(changeState);
            var d = serializer.Deserialize(bytes, changeState.GetType());
        }

        private void GlobalAfterDisposed(Disposed<StateClass> e)
        {
           
        }

        private void GlobalBeforeCreated(Created<StateClass> e)
        {
            if (e.GlobalKey == "foo")
            {

            }
        }

        private void AfterDisposed(Disposed<StateClass> e)
        {
            
        }

        private void BeforeDisposed(Disposed<StateClass> e)
        {
            
        }

        private void AfterCreated(Created<StateClass> e)
        {
            
        }

        private void BeforeCreated(Created<StateClass> e)
        {

        }

        private void BeforeAny(AnyOperation<StateClass> e)
        {

        }

        private void AfterAny(AnyOperation<StateClass> e)
        {

        }

        private void AfterHello(MethodCall<StateClass, int> e)
        {
            
        }

        private void BeforeHello(MethodCall<StateClass, int> e)
        {
        }

        public void BeforeHello()
        {
        }

        public void AfterHello()
        {
        }

        public void BeforeSizeChanged(PropertyUpdate<StateClass, int> change)
        {

        }

        public void AfterSizeChanged(PropertyUpdate<StateClass, int> change)
        {

        }

        static bool _init;
        [TestInitialize]
        public void Init()
        {
            if (!_init)
            {
                _init = true;
                App<MainTests>.Initialize();
            }
        }

        public IResolveTypes Initialize()
        {
            return new Resolver();
        }

        public class Resolver : IResolveTypes
        {
            List<KeyValuePair<Type, Tuple<Type, object>>> _types = new List<KeyValuePair<Type, Tuple<Type, object>>>();

            public Resolver()
            {
                _types.Add(new KeyValuePair<Type, Tuple<Type, object>>(
                    typeof(ISerializationContext),
                    new Tuple<Type, object>(typeof(ISerializationContext), new SerializationContext())));
                _types.Add(new KeyValuePair<Type, Tuple<Type, object>>(
                    typeof(IManagePersistentCollections),
                    new Tuple<Type, object>(typeof(IManagePersistentCollections), new PersistentCollectionManager())));
                _types.Add(new KeyValuePair<Type, Tuple<Type, object>>(
                    typeof(IBinarySerializerBuilder),
                    new Tuple<Type, object>(typeof(IBinarySerializerBuilder), new ILSerializerBuilder())));
                _types.Add(new KeyValuePair<Type, Tuple<Type, object>>(
                    typeof(ISerializer),
                    new Tuple<Type, object>(typeof(ISerializer), new ComplexSerializer(new ILSerializerBuilder()))));
                _types.Add(new KeyValuePair<Type, Tuple<Type, object>>(
                    typeof(ISerializer),
                    new Tuple<Type, object>(typeof(ISerializer), new MessageSegmentSerializer())));
                _types.Add(new KeyValuePair<Type, Tuple<Type, object>>(
                    typeof(IBestEffortChannelBuffer<UdpMessage>),
                    new Tuple<Type, object>(typeof(IBestEffortChannelBuffer<UdpMessage>), new BestEffortChannelBuffer())));
                _types.Add(new KeyValuePair<Type, Tuple<Type, object>>(
                    typeof(IChannelBuffer<UdpMessage>),
                    new Tuple<Type, object>(typeof(IChannelBuffer<UdpMessage>), new ChannelBuffer())));
                _types.Add(new KeyValuePair<Type, Tuple<Type, object>>(
                    typeof(IScheduler),
                    new Tuple<Type, object>(typeof(IScheduler), Scheduler.Current)));
                _types.Add(new KeyValuePair<Type, Tuple<Type, object>>(
                   typeof(IPublisher),
                   new Tuple<Type, object>(typeof(IPublisher), new FakePublisher())));
                _types.Add(new KeyValuePair<Type, Tuple<Type, object>>(
                   typeof(IObservableChannelProvider),
                   new Tuple<Type, object>(typeof(IObservableChannelProvider), new BestEffortObservableChannelProvider())));
            }

            public T Resolve<T>()
            {
                Tuple<Type, object> tuple = _types.Single(kvp => kvp.Key == typeof(T)).Value;
                return (T)tuple.Item2;
            }

            public IEnumerable<T> ResolveAll<T>()
            {
                foreach (var v in _types.Where(kvp => kvp.Key == typeof(T)))
                {
                    yield return (T)v.Value.Item2;
                }
            }
        }
    }
}
