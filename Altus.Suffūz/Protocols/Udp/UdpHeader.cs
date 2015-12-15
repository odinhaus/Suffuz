using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Altus.Suffūz.Serialization;

namespace Altus.Suffūz.Protocols.Udp
{
    public class UdpHeader : MessageSegment
    {
        private UdpHeader(byte[] data) : this(null, null, data) { }

        public UdpHeader(IChannel connection, EndPoint ep, byte[] data) : base(connection, Protocol.Udp, ep, data) { }
        /** ======================================================================================================================================
        * UDP HEADER DESCRIPTOR
        * FIELD                        LENGTH (bytes)      POS         SUBFIELDS/Description
        * TAG                          1                   0           VVVVVVSC - Version (6 bits), Segment Type (0 = Header, 1 = Segment), Compressed (0 = false, 1 = true)
        * SENDERID + MESSAGEID         8                   1           Combination of SENDER (16 bits) + MESSAGE SEQUENCE NUMBER (48 bits) = 64 bits
        * MESSAGEHASH                  16                  9           byte[] MD5 hash using secret hashkey + message body
        * SEGEMENTCOUNT                2                   25          total count of message segments, including header segment for complete message
        * TIMETOLIVE                   8                   27          absolute message expiration date/time in UTC for message reassembly to occur, before message is discarded
        * DATALENGTH                   2                   35          length in bytes of any included transfer data
        * DATA                         N (up to 1024 - 36) 37          included message data
        * =======================================================================================================================================
        * Total                        37 bytes            
        */
        protected override bool OnIsValid()
        {
            try
            {
                return this.SegmentLength <= this.Data.Length
                    && base.SegmentType == SegmentType.Header
                    && this.Sender > 0
                    && this.MessageId != 0;
            }
            catch { return false; }
        }

        private byte[] _hash;
        public byte[] MessageHash
        {
            get
            {
                if (_hash == null && Data != null)
                {
                    _hash = new byte[16];
                    for (int i = 0; i < 16; i++)
                        _hash[i] = Data[i + 9];
                }
                return _hash;
            }
        }
        private ushort _count = 0;
        public override ushort SegmentCount
        {
            get
            {
                if (_count == 0 && Data != null)
                {
                    _count = Data[25];
                }
                return _count;
            }
        }
        private TimeSpan _ttl = TimeSpan.MinValue;
        public override TimeSpan TimeToLive
        {
            get
            {
                if (_ttl == TimeSpan.MinValue && Data != null)
                {
                    _ttl = TimeSpan.FromMilliseconds(BitConverter.ToDouble(Data, 27));
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
                    _pl = BitConverter.ToUInt16(Data, 35);
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
                    Data.Copy(37, _plData, 0, PayloadLength);
                }
                return _plData;
            }
        }
        public int HeaderLength
        {
            get { return 37; }
        }

        public override int SegmentLength
        {
            get
            {
                return HeaderLength + PayloadLength;
            }
        }

        public override ushort SegmentNumber
        {
            get { return 1; }
        }
    }
}
