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
        public UdpHeader(IChannel connection, EndPoint ep, byte[] data) : base(connection, Protocol.Udp, ep, data) { }
        /** ======================================================================================================================================
            * UDP HEADER DESCRIPTOR
            * FIELD            LENGTH (bytes)      POS         SUBFIELDS/Description
            * TAG              1                   0           VVVVVVSC - Version (6 bits), Segment Type (0 = Header, 1 = Segment), Compressed (0 = false, 1 = true)
            * SENDERID         8                   1           Alpha-Numeric Unique Sender ID
            * MESSAGEID        16                  9           Sequential UINT per SENDER
            * MESSAGEHASH      16                  25          byte[] MD5 hash using secret hashkey + message body
            * SEGEMENTCOUNT    1                   41          total count of message segments, including header segment for complete message
            * TIMETOLIVE       8                   42          absolute message expiration date/time in UTC for message reassembly to occur, before message is discarded
            * DATALENGTH       2                   50          length in bytes of any included transfer data
            * DATA             N (up to 1024 - 48) 52            included message data
            * =======================================================================================================================================
            * Total            52 bytes            
            */
        protected override bool OnIsValid()
        {
            try
            {
                return this.SegmentLength <= this.Data.Length
                    && base.SegmentType == SegmentType.Header
                    && this.Sender > 0
                    && this.MessageId != Guid.Empty;
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
                        _hash[i] = Data[i + 25];
                }
                return _hash;
            }
        }
        private byte _count = 0;
        public byte SegmentCount
        {
            get
            {
                if (_count == 0 && Data != null)
                {
                    _count = Data[41];
                }
                return _count;
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
                        ttl[i] = Data[i + 42];
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
                    pl[0] = Data[50];
                    pl[1] = Data[51];
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
                    Data.Copy(52, _plData, 0, PayloadLength);
                }
                return _plData;
            }
        }
        public int HeaderLength
        {
            get { return 52; }
        }

        public int SegmentLength
        {
            get
            {
                return HeaderLength + PayloadLength;
            }
        }

        public override uint SegmentNumber
        {
            get { return 1; }
        }
    }
}
