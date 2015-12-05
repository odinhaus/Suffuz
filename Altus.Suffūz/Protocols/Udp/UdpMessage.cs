using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Protocols.Udp
{
    public class UdpMessage : IComparer<UdpSegment>
    {
        MD5 _hasher = MD5.Create();
        static object _lock = new object();

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
            this.Sender = segment.Sender;
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
             * TAG                          1                   0           VVVVVVSC - Version (6 bits), Segment Type (0 = Header, 1 = Segment), Compressed (0 = false, 1 = true)
             * SENDERID + MESSAGEID         8                   1           Combination of SENDER (16 bits) + MESSAGE SEQUENCE NUMBER (48 bits) = 64 bits
             * MESSAGEHASH                  16                  9           byte[] MD5 hash using secret hashkey + message body
             * SEGEMENTCOUNT                1                   25          total count of message segments, including header segment for complete message
             * TIMETOLIVE                   8                   26          absolute message expiration date/time in UTC for message reassembly to occur, before message is discarded
             * DATALENGTH                   2                   34          length in bytes of any included transfer data
             * DATA                         N (up to 1024 - 36) 36          included message data
             * =======================================================================================================================================
             * Total            36 bytes            
             */

            MemoryStream ms = new MemoryStream(source.ToByteArray());
            this.Sender = App.InstanceId;
            this.SequenceNumber = Connection.SequenceNumber;
            ushort headerLength = (ushort)Math.Min(ms.Length, SocketOptions.MTU_SIZE - 36);
            byte[] hdr = new byte[36];
            hdr[0] = (byte)0;
            byte[] nodeIdNum = BitConverter.GetBytes(this.MessageId);
            nodeIdNum.CopyTo(hdr, 1);

            byte[] data = new byte[headerLength];
            ms.Read(data, 0, headerLength);

            byte[] hdrData = new byte[hdr.Length + data.Length];
            byte[] secretData = App.InstanceCryptoKey;
            byte[] cryptoData = new byte[secretData.Length + data.Length];
            secretData.CopyTo(cryptoData, 0);
            data.CopyTo(cryptoData, secretData.Length);

            _hasher.ComputeHash(cryptoData).CopyTo(hdr, 9);
            byte segmentCount = 1;
            if (ms.Length > SocketOptions.MTU_SIZE - 36)
            {
                int len = (int)ms.Length - (SocketOptions.MTU_SIZE - 36);
                segmentCount += (byte)Math.Ceiling((float)len / (float)(SocketOptions.MTU_SIZE - 36));
            }
            hdr[25] = segmentCount;
            BitConverter.GetBytes(source.Timestamp.Add(source.TTL).ToBinary()).CopyTo(hdr, 26);
            BitConverter.GetBytes((ushort)data.Length).CopyTo(hdr, 34);
            hdr.CopyTo(hdrData, 0);
            data.CopyTo(hdrData, 36);

            var ths = new UdpHeader(Connection, Connection.EndPoint, hdrData);
            byte segNo = 2;
            while (ms.Position < ms.Length)
            {
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

                ushort segLength = (ushort)Math.Min(ms.Length - ms.Position, SocketOptions.MTU_SIZE - 20);
                byte[] seg = new byte[20];

                byte[] sdata = new byte[segLength];
                ms.Read(sdata, 0, segLength);

                seg[0] = (byte)(1 << 1);
                nodeIdNum.CopyTo(seg, 1);
                seg[9] = segNo;
                segNo++;
                BitConverter.GetBytes(source.Timestamp.Add(source.TTL).ToBinary()).CopyTo(seg, 10);
                BitConverter.GetBytes(segLength).CopyTo(seg, 18);

                byte[] segData = new byte[seg.Length + sdata.Length];
                seg.CopyTo(segData, 0);
                sdata.CopyTo(segData, 20);

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
        public ushort Sender { get; private set; }
        public ulong SequenceNumber { get; private set; }
        public ulong MessageId
        {
            get
            {
                ulong value = ((ulong)Sender << 48) + SequenceNumber;
                return value;
            }
            private set
            {
                Sender = (ushort)(value >> 48);
                SequenceNumber = (ulong)((value << 16) >> 16);
            }
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
            var position = 0;
            var length = BitConverter.ToUInt16(serialized, 34) + 36;
            
            var header = new UdpHeader(null, null, serialized.Take(length).ToArray());
            this.UdpHeaderSegment = header;

            position = length;

            while(position < serialized.Length)
            {
                length = BitConverter.ToUInt16(serialized, position + 18) + 20;
                this.UdpSegmentsPrivate.Add(new UdpSegment(null, null, serialized.Skip(position).Take(length).ToArray()));
                position += length;
            }
        }

        public byte[] ToBytes()
        {
            var bytes = new List<byte>();
            for(int i = 0; i<this.UdpSegmentsPrivate.Count; i++)
            {
                bytes.AddRange(this.UdpSegmentsPrivate[i].Data);
            }
            return bytes.ToArray();
        }
    }
}
