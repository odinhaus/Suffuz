using Altus.Suffūz.Collections;
using Altus.Suffūz.Scheduling;
using Altus.Suffūz.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Protocols.Udp
{
    public class BestEffortMulticastChannel : MulticastChannel
    {
        IPersistentDictionary<ushort, ulong> _sequenceNumbers;
        IPersistentDictionary<ulong, UdpMessage> _resendBuffer;

        public BestEffortMulticastChannel(IPEndPoint mcastGroup, bool listen) 
            : this(mcastGroup, listen, true)
        { }

        public BestEffortMulticastChannel(IPEndPoint mcastGroup, bool listen, bool excludeMessagesFromSelf)
            : this(new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp), mcastGroup, listen, excludeMessagesFromSelf)
        { }

        public BestEffortMulticastChannel(Socket udpSocket, IPEndPoint mcastGroup, bool listen, bool excludeMessagesFromSelf)
            : base(udpSocket, mcastGroup, listen, excludeMessagesFromSelf)
        {
            Initialize(mcastGroup);
        }

        public BestEffortMulticastChannel(IPEndPoint mcastGroup, bool listen, DataReceivedHandler handler)
            : this(mcastGroup, listen, true, handler)
        { }

        public BestEffortMulticastChannel(IPEndPoint mcastGroup, bool listen, bool excludeMessagesFromSelf, DataReceivedHandler handler)
            : this(new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp), mcastGroup, listen, excludeMessagesFromSelf, handler)
        { }


        public BestEffortMulticastChannel(Socket udpSocket, IPEndPoint mcastGroup, bool listen, bool excludeMessagesFromSelf, DataReceivedHandler handler)
            : base (udpSocket, mcastGroup, listen, excludeMessagesFromSelf, handler)
        {
            Initialize(mcastGroup);
        }

        protected virtual void Initialize(IPEndPoint mcastGroup)
        {
            var manager = App.Resolve<IManagePersistentCollections>();
            _sequenceNumbers = manager
                .GetOrCreate<IPersistentDictionary<ushort, ulong>>(
                    mcastGroup.ToString(),
                    (name) => new PersistentDictionary<ushort, ulong>(name, manager.GlobalHeap, true));

            ulong sequenceNumber;
            if (!_sequenceNumbers.TryGetValue(App.InstanceId, out sequenceNumber))
            {
                sequenceNumber = (ulong)(App.InstanceId << 48);
                _sequenceNumbers[App.InstanceId] = sequenceNumber;
            }
            SequenceNumber = sequenceNumber;

            App.Resolve<ISerializationContext>()
                .SetSerializer<UdpMessage, UdpMessageSerializer>(StandardFormats.BINARY);

            _resendBuffer = manager
                .GetOrCreate<IPersistentDictionary<ulong, UdpMessage>>(
                    mcastGroup.ToString(),
                    (name) => new PersistentDictionary<ulong, UdpMessage>(name, manager.GlobalHeap, false));
        }

        public override ServiceLevels ServiceLevels
        {
            get
            {
                return ServiceLevels.BestEffort;
            }
        }

       
        TimeSpan _ttl;
        public override TimeSpan DefaultTimeout
        {
            get
            {
                return _ttl;
            }

            set
            {
                _ttl = value;
            }
        }

        protected override void Cleaner()
        {
            _resendBuffer.Compact();
            base.Cleaner();
        }

        public override UdpMessage CreateUdpMessage(Message message)
        {
            var udpMessage = base.CreateUdpMessage(message);
            
            if (message.TTL.TotalMilliseconds > 0)
            {
                // add message to retry buffer
                _resendBuffer.Add(udpMessage.MessageId, udpMessage);
                // set the message to expire from the retry buffer based on the message's TTL
                Scheduler.Current.Schedule<ulong>(CurrentTime.Now.Add(message.TTL),
                    (messageId) => _resendBuffer.Remove(messageId),
                    () => udpMessage.MessageId);
            }

            return udpMessage;
        }
    }
}
