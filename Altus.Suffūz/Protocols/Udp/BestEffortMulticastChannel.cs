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
                _beBuffer = new BestEffortChannelBuffer();

            _beBuffer.Initialize(this);
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

            _beBuffer.AddRetryMessage(udpMessage);

            return udpMessage;
        }

        //protected override void ProcessInboundUdpSegment(MessageSegment segment)
        //{
        //    var messageId = _beBuffer[segment.Sender];

        //    if (messageId > 0)
        //    {
        //        ulong sequenceNo, lastSequenceNo;
        //        ushort senderId;

        //        UdpMessage.SplitMessageId(segment.MessageId, out senderId, out sequenceNo);
        //        UdpMessage.SplitMessageId(messageId, out senderId, out lastSequenceNo);

        //        if (lastSequenceNo < sequenceNo)
        //        {
        //            _beBuffer[senderId] = sequenceNo;

        //        }

                
        //        if (++lastSequenceNo == sequenceNo)
        //        {
        //            // this is the expected 
        //            // check we didn't miss an individual packet
        //            UdpMessage msg;
        //            if (_messages.TryGetValue(segment.MessageId, out msg))
        //            {
        //                // we've seen this message, check the segment number against our greatest segment number
        //                var lastSegment = _beBuffer[segment.MessageId];
        //                if (lastSegment < segment.SegmentNumber)
        //                {
        //                    _beBuffer[segment.MessageId] = segment.SegmentNumber;
        //                }


                        
        //            }
        //        }
        //        else if (lastSequenceNo < sequenceNo)
        //        {
        //            // we're 
        //            // we missed a whole message, need to request it again
        //            SendNACKMessages(lastSequenceNo, sequenceNo);
        //        }
        //    }

        //    base.ProcessInboundUdpSegment(segment);
        //}

        private void SendNACKMessages(ulong lastSequenceNo, ulong sequenceNo)
        {
            throw new NotImplementedException();
        }

        //protected override void ProcessCompletedInboundUdpMessage(UdpMessage udpMessage)
        //{
        //    // adds message and updates the last message Id for sender
        //    _beBuffer.AddRetryMessage(udpMessage);


        //    base.ProcessCompletedInboundUdpMessage(udpMessage);
        //}
    }
}
