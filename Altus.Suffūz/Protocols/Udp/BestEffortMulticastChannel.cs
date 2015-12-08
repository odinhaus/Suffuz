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
        IBestEffortChannelBuffer<UdpMessage> _beBuffer;

        public BestEffortMulticastChannel(string name, IPEndPoint mcastGroup, bool listen) 
            : this(name, mcastGroup, listen, true)
        { }

        public BestEffortMulticastChannel(string name, IPEndPoint mcastGroup, bool listen, bool excludeMessagesFromSelf)
            : this(name, new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp), mcastGroup, listen, excludeMessagesFromSelf)
        { }

        public BestEffortMulticastChannel(string name, Socket udpSocket, IPEndPoint mcastGroup, bool listen, bool excludeMessagesFromSelf)
            : base(name, udpSocket, mcastGroup, listen, excludeMessagesFromSelf)
        {
            Initialize(mcastGroup);
        }



        protected virtual void Initialize(IPEndPoint mcastGroup)
        {
            _beBuffer = App.Resolve<IBestEffortChannelBuffer<UdpMessage>>();
            if (_beBuffer == null)
                _beBuffer = new BestEffortChannelBuffer(this);

            _beBuffer.Initialize();

            SequenceNumber = _beBuffer.SequenceNumber;
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

        public override UdpMessage CreateUdpMessage(Message message)
        {
            var udpMessage = base.CreateUdpMessage(message);

            _beBuffer.AddMessage(udpMessage);

            return udpMessage;
        }
    }
}
