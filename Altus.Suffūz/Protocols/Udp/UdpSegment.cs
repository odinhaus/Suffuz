using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Altus.Suffūz.Serialization;

namespace Altus.Suffūz.Protocols.Udp
{
    public class UdpSegment : MessageSegment
    {
        private UdpSegment(byte[] data) : this(null, null, data) { }

        public UdpSegment(IChannel connection, EndPoint ep, byte[] data) : base(connection, Protocol.Udp, ep, data) { }
        /* =======================================================================================================================================
        * UDP SEGMENT DESCRIPTOR
        * FIELD                        LENGTH              POS     SUBFIELDS/Description
        * TAG                          1                   0       NNNNNNSN - Not Used (6 bits), Segment Type (0 = Header, 1 = Segment), Not Used (1 bit)
        * SENDERID + MESSAGEID         8                   1       Combination of SENDER (16 bits) + MESSAGE SEQUENCE NUMBER (48 bits) = 64 bits
        * SEGMENTNUMBER                2                   9       Segement sequence number 
        * SEGMENTCOUNT                 2                   11      Segment total count
        * TIMETOLIVE                   8                   13      Message segment expiration datetime
        * DATALENGTH                   2                   21      length in bytes of any included transfer data
        * DATA                         N (up to 1024 - 23) 23      included message data
        * =======================================================================================================================================
        * Total                        23 bytes     
        */
        protected override bool OnIsValid()
        {
            try
            {
                return this.SegmentLength <= this.Data.Length
                    && base.SegmentType == SegmentType.Segment
                    && this.Sender > 0
                    && this.MessageId != 0
                    && this.SegmentNumber > 0;
            }
            catch { return false; }
        }

        private ushort _segNo;
        public override ushort SegmentNumber
        {
            get
            {
                if (_segNo == 0 && Data != null)
                {
                    _segNo = BitConverter.ToUInt16(Data, 9);
                }
                return _segNo;
            }
        }

        private ushort _segCount;
        public override ushort SegmentCount
        {
            get
            {
                if (_segCount == 0 && Data != null)
                {
                    _segCount = BitConverter.ToUInt16(Data, 11);
                }
                return _segCount;
            }
        }

        private TimeSpan _ttl = TimeSpan.MinValue;
        public override TimeSpan TimeToLive
        {
            get
            {
                if (_ttl == TimeSpan.MinValue && Data != null)
                {
                    _ttl = TimeSpan.FromMilliseconds(BitConverter.ToDouble(Data, 13));
                }
                return _ttl;
            }
        }
        private ushort _pl;
        public override ushort PayloadLength
        {
            get
            {
                if (_pl == 0 && Data != null)
                {
                    _pl = BitConverter.ToUInt16(Data, 21);
                }
                return _pl;
            }
        }
        byte[] _plData;
        public override byte[] Payload
        {
            get
            {
                if (_plData == null && Data != null)
                {
                    _plData = new byte[PayloadLength];
                    Data.Copy(23, _plData, 0, PayloadLength);
                }
                return _plData;
            }
        }
        public int HeaderLength
        {
            get { return 23; }
        }

        public override int SegmentLength
        {
            get
            {
                return HeaderLength + PayloadLength;
            }
        }
    }
}
