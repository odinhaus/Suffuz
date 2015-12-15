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
using Altus.Suffūz.Routing;
using System.Threading;

namespace Altus.Suffūz.Collections.Tests
{
    [TestClass]
    public class ProtocolCollectionsTests : IBootstrapper
    {
        [TestMethod]
        public void CanSerializeUdpMessage()
        {
            var cb = App.Resolve<IBestEffortChannelBuffer<UdpMessage>>();
            var msg = new UdpMessage(cb.Channel, new Message(StandardFormats.BINARY, cb.Channel.Name, ServiceType.RequestResponse, App.InstanceName)
            {
                TTL = TimeSpan.FromSeconds(5),
                Payload = NoArgs.Empty,
                Recipients = new string[] { "*" }
            });

            var serializer = new UdpMessageSerializer();

            var serialized = serializer.Serialize(msg);
            var deserialized = serializer.Deserialize(serialized);

            Assert.IsTrue(msg.MessageId == deserialized.MessageId);
            Assert.IsTrue(msg.UdpHeaderSegment.PayloadLength == deserialized.UdpHeaderSegment.PayloadLength);
        }


        [TestMethod]
        public void CanInitializeMCastBuffer()
        {
            var cb = App.Resolve<IChannelBuffer<UdpMessage>>();
            cb.Initialize(new FakeChannel(cb));
            cb.Reset();
            Assert.IsTrue(cb.Channel.MessageId == (ulong)((ulong)App.InstanceId << 48));
            Assert.IsTrue(cb.IsInitialized);
        }

        [TestMethod]
        public void CanComposeMessageFromSegments()
        {
            var cb = App.Resolve<IChannelBuffer<UdpMessage>>();
            var sched = App.Resolve<IScheduler>();
            cb.Initialize(new FakeChannel(cb));
            cb.Reset();

            var msg = new UdpMessage(cb.Channel, new Message(StandardFormats.BINARY, cb.Channel.Name, ServiceType.RequestResponse, App.InstanceName)
            {
                TTL = TimeSpan.FromSeconds(2),
                Payload = NoArgs.Empty,
                Recipients = new string[] { "*" }
            });

            UdpMessage rcvd = null;
            cb.MessageReceived += (s, e) =>
            {
                rcvd = e.Message;
            };

            cb.AddInboundSegment(msg.UdpHeaderSegment);
            foreach(var seg in msg.UdpSegments)
            {
                cb.AddInboundSegment(seg);
            }

            Assert.IsTrue(rcvd != null && rcvd.MessageId == msg.MessageId);
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
                    typeof(ISerializer),
                    new Tuple<Type, object>(typeof(ISerializer), new MessageSegmentSerializer())));
                //_types.Add(new KeyValuePair<Type, Tuple<Type, object>>(
                //    typeof(IBestEffortChannelBuffer<UdpMessage>),
                //    new Tuple<Type, object>(typeof(IBestEffortChannelBuffer<UdpMessage>), new BestEffortChannelBuffer(new FakeChannel()))));
                _types.Add(new KeyValuePair<Type, Tuple<Type, object>>(
                    typeof(IChannelBuffer<UdpMessage>),
                    new Tuple<Type, object>(typeof(IChannelBuffer<UdpMessage>), new ChannelBuffer())));
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
        IChannelBuffer<UdpMessage> _buffer;
        public FakeChannel(IChannelBuffer<UdpMessage> buffer)
        {
            _buffer = buffer;
        }

        public TimeSpan DefaultTimeout { get; set; }

        public EndPoint EndPoint { get; set; }

        public string Format { get { return StandardFormats.BINARY; } }

        public bool IsDisconnected { get { return false; } }

        public string Name { get { return "fake_channel"; } }

        public Protocol Protocol { get { return Protocol.Udp; } }

        public ulong MessageId { get { return _buffer.LocalMessageId; } }
        public ulong SegmentId { get { return _buffer.LocalSegmentId; } }

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
