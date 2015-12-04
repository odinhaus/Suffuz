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
        public UdpSegment(IChannel connection, EndPoint ep, byte[] data) : base(connection, Protocol.Udp, ep, data) { }
        /* =======================================================================================================================================
        * UDP SEGMENT DESCRIPTOR
        * FIELD                        LENGTH              POS     SUBFIELDS/Description
        * TAG                          1                   0       NNNNNNSN - Not Used (6 bits), Segment Type (0 = Header, 1 = Segment), Not Used (1 bit)
        * SENDERID + MESSAGEID         8                   1       Combination of SENDER (16 bits) + MESSAGE SEQUENCE NUMBER (48 bits) = 64 bits
        * SEGMENTNUMBER                1                   9      Segement sequence number 
        * TIMETOLIVE                   8                   10      Message segment expiration datetime
        * DATALENGTH                   2                   18      length in bytes of any included transfer data
        * DATA                         N (up to 1024 - 23) 20      included message data
        * =======================================================================================================================================
        * Total                        20 bytes     
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

        private uint _segNo;
        public override uint SegmentNumber
        {
            get
            {
                if (_segNo == 0 && Data != null)
                {
                    _segNo = Data[9];
                }
                return _segNo;
            }
        }
        private DateTime _ttl = DateTime.MinValue;
        public DateTime TimeToLive
        {
            get
            {
                if (_ttl == DateTime.MinValue && Data != null)
                {
                    byte[] ttl = new byte[8];
                    for (int i = 0; i < 8; i++)
                        ttl[i] = Data[i + 10];
                    _ttl = DateTime.FromBinary(BitConverter.ToInt64(ttl, 0)).ToLocalTime();
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
                    byte[] pl = new byte[2];
                    pl[0] = Data[18];
                    pl[1] = Data[19];
                    _pl = BitConverter.ToUInt16(pl, 0);
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
                    Data.Copy(20, _plData, 0, PayloadLength);
                }
                return _plData;
            }
        }
        public int HeaderLength
        {
            get { return 20; }
        }

        public int SegmentLength
        {
            get
            {
                return HeaderLength + PayloadLength;
            }
        }
    }
}
