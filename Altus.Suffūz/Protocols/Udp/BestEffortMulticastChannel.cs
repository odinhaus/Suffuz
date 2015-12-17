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
        public BestEffortMulticastChannel(IBestEffortChannelBuffer<UdpMessage> buffer, string name, IPEndPoint mcastGroup, bool listen, int ttl) 
            : this(buffer, name, mcastGroup, listen, true, ttl)
        { }

        public BestEffortMulticastChannel(IBestEffortChannelBuffer<UdpMessage> buffer, string name, IPEndPoint mcastGroup, bool listen, bool excludeMessagesFromSelf, int ttl)
            : this(buffer, name, new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp), mcastGroup, listen, excludeMessagesFromSelf, ttl)
        { }

        public BestEffortMulticastChannel(IBestEffortChannelBuffer<UdpMessage> buffer, string name, Socket udpSocket, IPEndPoint mcastGroup, bool listen, bool excludeMessagesFromSelf, int ttl)
            : base(buffer, name, udpSocket, mcastGroup, listen, excludeMessagesFromSelf, ttl)
        {
            buffer.MissedSegments += Buffer_MissedSegments;
            buffer.ResendSegment += Buffer_ResendSegment;
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


        public new IBestEffortChannelBuffer<UdpMessage> Buffer { get { return (IBestEffortChannelBuffer<UdpMessage>)base.Buffer; } }

        protected override void SendSegment(MessageSegment segment)
        {
            if (segment.MessageId > 0) // ignore special segments like NAKs
            {
                Buffer.AddRetrySegment(segment);
            }
            base.SendSegment(segment);
        }

        private void Buffer_MissedSegments(object sender, MissedSegmentsEventArgs e)
        {
            // send a NAK for the missed packets
            var nak = new UdpSegmentNAK(e.SenderId, e.RecipientId, e.StartSegmentId, e.EndSegmentId);
            SendNAK(nak);
        }

        private void Buffer_ResendSegment(object sender, ResendSegmentEventArgs e)
        {
            this.SendSegment(e.Segment);
        }

        protected virtual void SendNAK(UdpSegmentNAK nak)
        {
            this.SendSegment(nak);
        }
    }
}
