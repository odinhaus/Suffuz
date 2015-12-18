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
            foreach (var seg in msg.UdpSegments)
            {
                cb.AddInboundSegment(seg);
            }

            Assert.IsTrue(rcvd != null && rcvd.MessageId == msg.MessageId);
        }

        [TestMethod]
        public void CanDetectAndRequestMissedPacketsInChannelBuffer()
        {
            var cb = App.Resolve<IBestEffortChannelBuffer<UdpMessage>>();
            var channel = new FakeBEChannel(cb);
            cb.Initialize(channel);
            cb.Reset();

            var request = new ChannelRequest<byte[], byte[]>("test")
            {
                Payload = new byte[1300 * 5]
            };

            channel.Call(request);
            var msg = channel.SentUdpMessage;

            cb.AddInboundSegment(msg.UdpSegments[2]);
            Assert.IsTrue(channel.MissedSegments.StartSegmentId == msg.UdpHeaderSegment.SegmentId);
            Assert.IsTrue(channel.MissedSegments.EndSegmentId == msg.UdpSegments[2].SegmentId);
            Assert.IsTrue(channel.MissedSegments.SenderId == msg.UdpHeaderSegment.Sender);
            Assert.IsTrue(channel.MissedSegments.RecipientId == App.InstanceId);
            Assert.IsTrue(channel.MessageReceived == null);
            Assert.IsTrue(channel.ResendSegment.Segment.SegmentId == msg.UdpSegments[1].SegmentId);

            channel.MissedSegments = null;
            Thread.Sleep(250);
            cb.AddInboundSegment(msg.UdpHeaderSegment);
            Assert.IsTrue(channel.MissedSegments.StartSegmentId == msg.UdpSegments[0].SegmentId);
            Assert.IsTrue(channel.MissedSegments.EndSegmentId == msg.UdpSegments[2].SegmentId);
            Assert.IsTrue(channel.MissedSegments.SenderId == msg.UdpHeaderSegment.Sender);
            Assert.IsTrue(channel.MissedSegments.RecipientId == App.InstanceId);
            Assert.IsTrue(channel.MessageReceived == null);
            Assert.IsTrue(channel.ResendSegment.Segment.SegmentId == msg.UdpSegments[1].SegmentId);

            channel.MissedSegments = null;
            Thread.Sleep(250);
            cb.AddInboundSegment(msg.UdpSegments[0]);
            Assert.IsTrue(channel.MissedSegments.StartSegmentId == msg.UdpSegments[1].SegmentId);
            Assert.IsTrue(channel.MissedSegments.EndSegmentId == msg.UdpSegments[2].SegmentId);
            Assert.IsTrue(channel.MissedSegments.SenderId == msg.UdpHeaderSegment.Sender);
            Assert.IsTrue(channel.MissedSegments.RecipientId == App.InstanceId);
            Assert.IsTrue(channel.MessageReceived == null);
            Assert.IsTrue(channel.ResendSegment.Segment.SegmentId == msg.UdpSegments[1].SegmentId);

            channel.MissedSegments = null;
            cb.AddInboundSegment(msg.UdpSegments[1]);
            Assert.IsTrue(channel.MissedSegments == null);
            Assert.IsTrue(channel.MessageReceived == null);


            var msg2 = new UdpMessage(cb.Channel, new Message(StandardFormats.BINARY, cb.Channel.Name, ServiceType.RequestResponse, App.InstanceName)
            {
                TTL = TimeSpan.FromSeconds(2),
                Payload = new byte[400], // make sure we have several packets
                Recipients = new string[] { "*" }
            });

            var request2 = new ChannelRequest<byte[], byte[]>("test")
            {
                Payload = new byte[400]
            };

            channel.Call(request2);

            channel.MissedSegments = null;
            cb.AddInboundSegment(msg2.UdpHeaderSegment);
            Assert.IsTrue(channel.MissedSegments.StartSegmentId == msg.UdpSegments[3].SegmentId);
            Assert.IsTrue(channel.MissedSegments.EndSegmentId == msg2.UdpHeaderSegment.SegmentId);
            Assert.IsTrue(channel.MissedSegments.SenderId == msg.UdpHeaderSegment.Sender);
            Assert.IsTrue(channel.MissedSegments.RecipientId == App.InstanceId);
            Assert.IsTrue(channel.MessageReceived == null);

            channel.MissedSegments = null;
            cb.AddInboundSegment(msg.UdpHeaderSegment); // check we ignore packets we already have
            Assert.IsTrue(channel.MissedSegments.StartSegmentId == msg.UdpSegments[3].SegmentId);
            Assert.IsTrue(channel.MissedSegments.EndSegmentId == msg2.UdpHeaderSegment.SegmentId);
            Assert.IsTrue(channel.MissedSegments.SenderId == msg.UdpHeaderSegment.Sender);
            Assert.IsTrue(channel.MissedSegments.RecipientId == App.InstanceId);
            Assert.IsTrue(channel.MessageReceived == null);

            channel.MissedSegments = null;
            cb.AddInboundSegment(msg.UdpSegments[3]);
            Assert.IsTrue(channel.MissedSegments.StartSegmentId == msg.UdpSegments[4].SegmentId);
            Assert.IsTrue(channel.MissedSegments.EndSegmentId == msg2.UdpHeaderSegment.SegmentId);
            Assert.IsTrue(channel.MissedSegments.SenderId == msg.UdpHeaderSegment.Sender);
            Assert.IsTrue(channel.MissedSegments.RecipientId == App.InstanceId);
            Assert.IsTrue(channel.MessageReceived == null);

            channel.MissedSegments = null;
            cb.AddInboundSegment(msg.UdpSegments[4]);
            Assert.IsTrue(channel.MissedSegments == null);
            Assert.IsTrue(channel.MessageReceived != null);
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
                _types.Add(new KeyValuePair<Type, Tuple<Type, object>>(
                    typeof(IBestEffortChannelBuffer<UdpMessage>),
                    new Tuple<Type, object>(typeof(IBestEffortChannelBuffer<UdpMessage>), new BestEffortChannelBuffer())));
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
                foreach (var v in _types.Where(kvp => kvp.Key == typeof(T)))
                {
                    yield return (T)v.Value.Item2;
                }
            }
        }
    }

    public class FakeChannel : IChannel
    {
        protected IChannelBuffer<UdpMessage> _buffer;
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

        public virtual ServiceLevels ServiceLevels { get { return ServiceLevels.Default; } }
        public Message SentMessage { get; set; }
        public UdpMessage SentUdpMessage { get; set; }
        public Encoding TextEncoding { get; set; }

        public event EventHandler Disconnected;
        public event EventHandler Disposed;
        public event EventHandler Disposing;

        public TResponse Call<TRequest, TResponse>(ChannelRequest<TRequest, TResponse> request)
        {
            return Call(request, null);
        }

        public TResponse Call<TRequest, TResponse>(ChannelRequest<TRequest, TResponse> request, Func<TResponse, bool> handler)
        {
            var message = new Message(Format, request.Uri, ServiceType.RequestResponse, App.InstanceName)
            {
                Payload = request.Payload,
                Recipients = request.Recipients
            };
            
            var response = Call(message, request.Timeout, handler);
            return default(TResponse);
        }

        public virtual Message Call<TResponse>(Message message, TimeSpan timeout, Func<TResponse, bool> handler)
        {
            this.SentMessage = message;
            this.Send(message);
            return null;
        }

        public virtual void Send(Message message)
        {
            this.TextEncoding = Encoding.Unicode;

            if (message.Encoding == null)
                message.Encoding = this.TextEncoding.EncodingName;

            App.Resolve<ISerializationContext>().TextEncoding = Encoding.GetEncoding(message.Encoding);
            UdpMessage tcpMsg = CreateUdpMessage(message);

            this.SendSegment(tcpMsg.UdpHeaderSegment);

            for (int i = 0; i < tcpMsg.UdpSegments.Length; i++)
            {
                this.SendSegment(tcpMsg.UdpSegments[i]);
            }
        }

        public virtual UdpMessage CreateUdpMessage(Message message)
        {
            _buffer.IncrementLocalMessageId();
            this.SentUdpMessage = new UdpMessage(this, message);
            return this.SentUdpMessage;
        }

        protected virtual void SendSegment(MessageSegment segment)
        {
            var serializationContext = App.Resolve<ISerializationContext>();
            var serializer = serializationContext.GetSerializer<ComplexPOCO>(StandardFormats.BINARY);
            var bytes = serializer.Serialize(new ComplexPOCO());
            var poco = serializer.Deserialize(bytes);
        }

        public void Dispose()
        {
        }

        public void ResetProperties()
        {
        }
    }

    public class FakeBEChannel : FakeChannel
    {
        public FakeBEChannel(IBestEffortChannelBuffer<UdpMessage> buffer) : base(buffer)
        {
            buffer.MissedSegments += Buffer_MissedSegments;
            buffer.MessageReceived += Buffer_MessageReceived;
            buffer.ResendSegment += Buffer_ResendSegment;
        }

        private void Buffer_ResendSegment(object sender, ResendSegmentEventArgs e)
        {
            ResendSegment = e;
        }

        private void Buffer_MessageReceived(object sender, MessageAvailableEventArgs<UdpMessage> e)
        {
            MessageReceived = e;
        }

        private void Buffer_MissedSegments(object sender, MissedSegmentsEventArgs e)
        {
            MissedSegments = e;
            _buffer.AddInboundSegment(new UdpSegmentNAK(e.SenderId, e.RecipientId, e.StartSegmentId, e.EndSegmentId));
        }

        public MissedSegmentsEventArgs MissedSegments { get; set; }

        public MessageAvailableEventArgs<UdpMessage> MessageReceived { get; set; }

        public ResendSegmentEventArgs ResendSegment { get; set; }


        public override ServiceLevels ServiceLevels
        {
            get
            {
                return ServiceLevels.BestEffort;
            }
        }

        protected override void SendSegment(MessageSegment segment)
        {
            if (segment.MessageId > 0) // ignore special segments like NAKs
            {
                ((IBestEffortChannelBuffer < UdpMessage >)_buffer).AddRetrySegment(segment);
            }
            base.SendSegment(segment);
        }
    }
}
