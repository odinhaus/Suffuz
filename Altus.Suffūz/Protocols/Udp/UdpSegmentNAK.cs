using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Protocols.Udp
{
    public class UdpSegmentNAK : MessageSegment
    {
        public UdpSegmentNAK(ushort sender, ushort recipient, ulong startSegmentId, ulong endSegmentId)
            : this(GetNAKBytes(sender, recipient, startSegmentId, endSegmentId))
        {

        }

        public UdpSegmentNAK(byte[] data) 
            : this(null, null, data) { }

        public UdpSegmentNAK(IChannel channel, EndPoint endPoint, byte[] data)
            : base(channel, Protocol.Udp, endPoint, data)
        { }

        /* =======================================================================================================================================
        * UDP SEGMENT DESCRIPTOR
        * FIELD                        LENGTH              POS     SUBFIELDS/Description
        * TAG                          1                   0       Segment Type (0 = Header, 1 = Segment)
        * SENDERID + MESSAGEID         8                   1       Combination of SENDER (16 bits) + MESSAGE SEQUENCE NUMBER (48 bits) = 64 bits
        * SEGMENTID                    8                   9       Unique incremental ulong per packet
        * RECIPIENT                    2                   11      The sender the NAK is directed to
        * SEGMENT START                8                   19      Segement sequence number 
        * SEGMENT END                  8                   27      Segment total count
        * =======================================================================================================================================
        * Total                        35 bytes     
        */
        byte[] _payload = new byte[0];
        public override byte[] Payload
        {
            get
            {
                return _payload;
            }
        }

        public override ushort PayloadLength
        {
            get
            {
                return 0;
            }
        }

        public override ushort SegmentCount
        {
            get
            {
                return 1;
            }
        }

        public override int SegmentLength
        {
            get
            {
                return 35;
            }
        }

        public override ushort SegmentNumber
        {
            get
            {
                return 1;
            }
        }

        public override TimeSpan TimeToLive
        {
            get
            {
                return TimeSpan.FromMilliseconds(0);
            }
        }

        ulong _segmentStart = 0;
        public ulong SegmentStart
        {
            get
            {
                if (_segmentStart == 0 && Data != null)
                {
                    _segmentStart = BitConverter.ToUInt64(Data, 19);
                }
                return _segmentStart;
            }
        }

        ulong _segmentEnd = 0;
        public ulong SegmentEnd
        {
            get
            {
                if (_segmentEnd == 0 && Data != null)
                {
                    _segmentEnd = BitConverter.ToUInt64(Data, 27);
                }
                return _segmentEnd;
            }
        }

        protected override bool OnIsValid()
        {
            return this.MessageId == 0
                && this.SegmentId == 0
                && this.SegmentEnd >= this.SegmentStart
                && this.SegmentStart > 0;
        }

        public static byte[] GetNAKBytes(ushort sender, ushort recipient, ulong startSegmentId, ulong endSegmentId)
        {
            var bytes = new byte[35];
            bytes[0] = (byte)2;
            BitConverter.GetBytes((ulong)((ulong)sender << 48)).CopyTo(bytes, 1);
            BitConverter.GetBytes((ulong)0).CopyTo(bytes, 9);
            BitConverter.GetBytes(recipient).CopyTo(bytes, 17);
            BitConverter.GetBytes(startSegmentId).CopyTo(bytes, 19);
            BitConverter.GetBytes(endSegmentId).CopyTo(bytes, 25);
            return bytes;
        }
    }
}
