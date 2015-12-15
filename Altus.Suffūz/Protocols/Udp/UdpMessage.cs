using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Protocols.Udp
{
    public class UdpMessage : IComparer<UdpSegment>, IProtocolMessage<UdpSegment>
    {
        MD5 _hasher = MD5.Create();
        static object _lock = new object();

        public static void SplitMessageId(ulong messageId, out ushort senderId, out ulong sequenceNumber)
        {
            senderId = (ushort)(messageId >> 48);
            sequenceNumber = ((messageId << 16)) >> 16;
        }

        internal UdpMessage(byte[] serialized)
        {
            this.FromBytes(serialized);
        }

        public UdpMessage(IChannel connection, Message source)
        {
            UdpSegmentsPrivate = new List<UdpSegment>();
            Connection = connection;
            FromMessage(source);
        }

        public UdpMessage(IChannel connection, MessageSegment segment)
        {
            UdpSegmentsPrivate = new List<UdpSegment>();
            Connection = connection;
            this.MessageId = segment.MessageId;
            if (segment is UdpHeader)
            {
                this.UdpHeaderSegment = (UdpHeader)segment;
            }
            else if (segment is UdpSegment)
            {
                this.AddSegment((UdpSegment)segment);
            }
        }


        private void FromMessage(Message source)
        {
            /** ======================================================================================================================================
             * UDP HEADER DESCRIPTOR
             * FIELD                        LENGTH (bytes)      POS         SUBFIELDS/Description
             * TAG                          1                   0           Segment Type (0 = Header, 1 = Segment)
             * SENDERID + MESSAGEID         8                   1           Combination of SENDER (16 bits) + MESSAGE SEQUENCE NUMBER (48 bits) = 64 bits
             * MESSAGEHASH                  16                  9           byte[] MD5 hash using secret hashkey + message body
             * SEGEMENTCOUNT                2                   25          total count of message segments, including header segment for complete message
             * TIMETOLIVE                   8                   27          absolute message expiration date/time in UTC for message reassembly to occur, before message is discarded
             * DATALENGTH                   2                   35          length in bytes of any included transfer data
             * DATA                         N (up to 1024 - 36) 37          included message data
             * =======================================================================================================================================
             * Total            37 bytes            
             */

            MemoryStream ms = new MemoryStream(source.ToByteArray());
            this.MessageId = Connection.MessageId;
            ushort headerLength = (ushort)Math.Min(ms.Length, SocketOptions.MTU_SIZE - 37);
            byte[] hdr = new byte[37];
            hdr[0] = (byte)0;
            byte[] messageId = BitConverter.GetBytes(this.MessageId);
            messageId.CopyTo(hdr, 1);

            byte[] data = new byte[headerLength];
            ms.Read(data, 0, headerLength);

            byte[] hdrData = new byte[hdr.Length + data.Length];
            byte[] secretData = App.InstanceCryptoKey;
            byte[] cryptoData = new byte[secretData.Length + data.Length];
            secretData.CopyTo(cryptoData, 0);
            data.CopyTo(cryptoData, secretData.Length);

            _hasher.ComputeHash(cryptoData).CopyTo(hdr, 9);
            ushort segmentCount = 1;
            if (ms.Length > SocketOptions.MTU_SIZE - 37)
            {
                int len = (int)ms.Length - (SocketOptions.MTU_SIZE - 37);
                segmentCount += (byte)Math.Ceiling((float)len / (float)(SocketOptions.MTU_SIZE - 37));
            }
            BitConverter.GetBytes(segmentCount).CopyTo(hdr, 25);
            BitConverter.GetBytes(source.TTL.TotalMilliseconds).CopyTo(hdr, 27);
            BitConverter.GetBytes((ushort)data.Length).CopyTo(hdr, 35);
            hdr.CopyTo(hdrData, 0);
            data.CopyTo(hdrData, 37);

            var ths = new UdpHeader(Connection, Connection.EndPoint, hdrData);
            ushort segNo = 2;
            while (ms.Position < ms.Length)
            {
                /* =======================================================================================================================================
                * UDP SEGMENT DESCRIPTOR
                * FIELD                        LENGTH              POS     SUBFIELDS/Description
                * TAG                          1                   0       Segment Type (0 = Header, 1 = Segment)
                * SENDERID + MESSAGEID         8                   1       Combination of SENDER (16 bits) + MESSAGE SEQUENCE NUMBER (48 bits) = 64 bits
                * SEGMENTID                    8                   9       Unique incremental ulong per packet
                * SEGMENTNUMBER                2                   17      Segement sequence number 
                * SEGMENTCOUNT                 2                   19      Segment total count
                * TIMETOLIVE                   8                   21      Message segment expiration datetime
                * DATALENGTH                   2                   29      length in bytes of any included transfer data
                * DATA                         N (up to 1024 - 23) 31      included message data
                * =======================================================================================================================================
                * Total                        31 bytes     
                */

                ushort segLength = (ushort)Math.Min(ms.Length - ms.Position, SocketOptions.MTU_SIZE - 31);
                byte[] segment = new byte[31];

                byte[] sdata = new byte[segLength];
                ms.Read(sdata, 0, segLength);

                segment[0] = (byte)(1 << 1);
                messageId.CopyTo(segment, 1);
                BitConverter.GetBytes(Connection.SegmentId).CopyTo(segment, 9);
                BitConverter.GetBytes(segNo).CopyTo(segment, 17);
                BitConverter.GetBytes(segmentCount).CopyTo(segment, 19);
                segNo++;
                BitConverter.GetBytes(source.TTL.TotalMilliseconds).CopyTo(segment, 21);
                BitConverter.GetBytes(segLength).CopyTo(segment, 29);

                byte[] segData = new byte[segment.Length + sdata.Length];
                segment.CopyTo(segData, 0);
                sdata.CopyTo(segData, 31);

                UdpSegment tss = new UdpSegment(Connection, Connection.EndPoint, segData);
                this.UdpSegmentsPrivate.Add(tss);
            }
            this.UdpHeaderSegment = ths;
        }


        public IChannel Connection { get; private set; }
        public UdpHeader UdpHeaderSegment { get; set; }
        private List<UdpSegment> UdpSegmentsPrivate { get; set; }
        bool _sorted = false;
        public UdpSegment[] UdpSegments
        {
            get
            {
                if (!_sorted)
                {
                    UdpSegmentsPrivate.Sort(this);
                    _sorted = true;
                }
                return UdpSegmentsPrivate.ToArray();
            }
        }
        public ushort Sender { get { return (ushort)(MessageId >> 48); } }
        public ulong MessageId
        {
            get;
            private set;
        }

        public bool IsComplete
        {
            get
            {
                return this.UdpHeaderSegment != null
                    && (this.UdpHeaderSegment.SegmentCount == UdpSegmentsPrivate.Count + 1);
            }
        }

        Stream _payload = null;
        public Stream Payload
        {
            get
            {
                if (_payload == null && IsComplete)
                {
                    UdpSegment[] segments = UdpSegments;
                    MemoryStream ms = new MemoryStream();
                    ms.Write(UdpHeaderSegment.Payload, 0, UdpHeaderSegment.PayloadLength);
                    for (int i = 0; i < segments.Length; i++)
                    {
                        ms.Write(segments[i].Payload, 0, segments[i].PayloadLength);
                    }
                    ms.Position = 0;
                    _payload = ms;
                }
                return _payload;
            }
        }

        public void AddSegment(UdpSegment segment)
        {
            if (segment.MessageId == 0) return;
            UdpSegmentsPrivate.Add(segment);
            _sorted = false;
        }

        public int Compare(UdpSegment x, UdpSegment y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;
            return x.SegmentNumber.CompareTo(y.SegmentNumber);
        }

        protected void FromBytes(byte[] serialized)
        {
            this.UdpSegmentsPrivate = new List<UdpSegment>();
            var position = 0;
            var length = BitConverter.ToUInt16(serialized, 34) + 36;
            
            var header = new UdpHeader(null, null, serialized.Take(length).ToArray());
            this.UdpHeaderSegment = header;
            this.MessageId = header.MessageId;

            position = length;

            while(position < serialized.Length)
            {
                length = BitConverter.ToUInt16(serialized, position + 18) + 20;
                this.UdpSegmentsPrivate.Add(new UdpSegment(null, null, serialized.Skip(position).Take(length).ToArray()));
                position += length;
            }
            this.UdpSegmentsPrivate.Sort(this);
        }

        public byte[] ToBytes()
        {
            var bytes = new List<byte>();
            bytes.AddRange(this.UdpHeaderSegment.Data);
            for(int i = 0; i<this.UdpSegmentsPrivate.Count; i++)
            {
                bytes.AddRange(this.UdpSegmentsPrivate[i].Data);
            }
            return bytes.ToArray();
        }
    }
}
