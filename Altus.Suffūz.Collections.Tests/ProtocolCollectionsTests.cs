using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Altus.Suffūz.Serialization;
using Altus.Suffūz.Protocols.Udp;
using Altus.Suffūz.Scheduling;
using Altus.Suffūz.DependencyInjection;
using System.Collections.Generic;
using System.Linq;
using Altus.Suffūz.Serialization.Binary;
using Altus.Suffūz.Protocols;
using System.Net;
using System.Text;

namespace Altus.Suffūz.Collections.Tests
{
    [TestClass]
    public class ProtocolCollectionsTests : IBootstrapper
    {
       

        [TestMethod]
        public void CanInitializeMCastBuffers()
        {
            var cb = App.Resolve<IBestEffortChannelBuffer<UdpMessage>>();
            var sched = App.Resolve<IScheduler>();
            cb.Initialize();
            cb.Reset();
            Assert.IsTrue(cb.SequenceNumber == (ulong)((ulong)App.InstanceId << 48));
            Assert.IsTrue(cb.IsInitialized);
            Assert.IsTrue(sched.Count() == 0);
        }

        
        static bool _init;
        [TestInitialize]
        public void Init()
        {
            if (!_init)
            {
                _init = true;
                App<ProtocolCollectionsTests>.Initialize();
            }
        }

        public IResolveTypes Initialize()
        {
            return new Resolver();
        }

        public byte[] InstanceCryptoKey
        {
            get
            {
                return new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            }
        }

        public ushort InstanceId
        {
            get
            {
                return 1;
            }
        }

        public string InstanceName
        {
            get
            {
                return "test1";
            }
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
                    typeof(IBestEffortChannelBuffer<UdpMessage>),
                    new Tuple<Type, object>(typeof(IBestEffortChannelBuffer<UdpMessage>), new BestEffortChannelBuffer(new FakeChannel()))));
                _types.Add(new KeyValuePair<Type, Tuple<Type, object>>(
                    typeof(IScheduler),
                    new Tuple<Type, object>(typeof(IScheduler), Scheduler.Current)));
            }

            public T Resolve<T>()
            {
                Tuple<Type, object> tuple = _types.Single(kvp => kvp.Key == typeof(T)).Value;
                return (T)tuple.Item2;
            }

            public IEnumerable<T> ResolveAll<T>()
            {
                foreach(var v in _types.Where(kvp => kvp.Key == typeof(T)))
                {
                    yield return (T)v.Value.Item2;
                }
            }
        }
    }

    public class FakeChannel : IChannel
    {
        public TimeSpan DefaultTimeout { get; set; }

        public EndPoint EndPoint { get; set; }

        public string Format { get { return StandardFormats.BINARY; } }

        public bool IsDisconnected { get { return false; } }

        public string Name { get { return "fake_channel"; } }

        public Protocol Protocol { get { return Protocol.Udp; } }

        public ulong SequenceNumber { get; set; }

        public ServiceLevels ServiceLevels { get { return ServiceLevels.BestEffort; } }

        public Encoding TextEncoding { get; set; }

        public event EventHandler Disconnected;
        public event EventHandler Disposed;
        public event EventHandler Disposing;

        public TResponse Call<TRequest, TResponse>(ChannelRequest<TRequest, TResponse> request)
        {
            throw new NotImplementedException();
        }

        public TResponse Call<TRequest, TResponse>(ChannelRequest<TRequest, TResponse> request, Func<TResponse, bool> handler)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void ResetProperties()
        {
            throw new NotImplementedException();
        }
    }
}
