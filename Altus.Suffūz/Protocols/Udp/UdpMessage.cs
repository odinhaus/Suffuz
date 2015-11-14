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

        public UdpMessage(IChannel connection)
        {
            UdpSegmentsPrivate = new List<UdpSegment>();
            Connection = connection;
            this.MessageId = Guid.NewGuid();
        }

        public UdpMessage(IChannel connection, Message source) : this(connection)
        {
            FromMessage(source);
        }

        public UdpMessage(IChannel connection, MessageSegment segment)
            : this(connection)
        {
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
            MemoryStream ms = new MemoryStream(source.ToByteArray());
            this.Sender = App.InstanceId;
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

            ushort headerLength = (ushort)Math.Min(ms.Length, SocketOptions.MTU_SIZE - 52);
            byte[] hdr = new byte[52];
            hdr[0] = (byte)0;
            byte[] nodeIdNum = BitConverter.GetBytes(this.Sender);
            byte[] msgIdNum = this.MessageId.ToByteArray();
            nodeIdNum.CopyTo(hdr, 1);
            msgIdNum.CopyTo(hdr, 9);


            byte[] data = new byte[headerLength];
            ms.Read(data, 0, headerLength);

            byte[] hdrData = new byte[hdr.Length + data.Length];
            byte[] secretData = App.InstanceCryptoKey;
            byte[] cryptoData = new byte[secretData.Length + data.Length];
            secretData.CopyTo(cryptoData, 0);
            data.CopyTo(cryptoData, secretData.Length);

            _hasher.ComputeHash(cryptoData).CopyTo(hdr, 25);
            byte segmentCount = 1;
            if (ms.Length > SocketOptions.MTU_SIZE - 52)
            {
                int len = (int)ms.Length - (SocketOptions.MTU_SIZE - 52);
                segmentCount += (byte)Math.Ceiling((float)len / (float)(SocketOptions.MTU_SIZE - 36));
            }
            hdr[41] = segmentCount;
            BitConverter.GetBytes(source.Timestamp.Add(source.TTL).ToBinary()).CopyTo(hdr, 42);
            BitConverter.GetBytes((ushort)data.Length).CopyTo(hdr, 50);
            hdr.CopyTo(hdrData, 0);
            data.CopyTo(hdrData, 52);

            var ths = new UdpHeader(Connection, Connection.EndPoint, hdrData);
            byte segNo = 2;
            while (ms.Position < ms.Length)
            {
                /* =======================================================================================================================================
                 * UDP SEGMENT DESCRIPTOR
                 * FIELD            LENGTH              POS     SUBFIELDS/Description
                 * TAG              1                   0       NNNNNNSN - Not Used (6 bits), Segment Type (0 = Header, 1 = Segment), Not Used (1 bit)
                 * SENDERID         8                   1       Alpha-Numeric Unique Sender ID
                 * MESSAGEID        16                  9       Sequential UINT per SENDER
                 * SEGMENTNUMBER    1                   25      Segement sequence number 
                 * TIMETOLIVE       8                   26      Message segment expiration datetime
                 * DATALENGTH       2                   34      length in bytes of any included transfer data
                 * DATA             N (up to 1024 - 23) 36      included message data
                 * =======================================================================================================================================
                 * Total            36 bytes     
                 */

                ushort segLength = (ushort)Math.Min(ms.Length - ms.Position, SocketOptions.MTU_SIZE - 36);
                byte[] seg = new byte[36];

                byte[] sdata = new byte[segLength];
                ms.Read(sdata, 0, segLength);

                seg[0] = (byte)(1 << 1);
                nodeIdNum.CopyTo(seg, 1);
                msgIdNum.CopyTo(seg, 9);
                seg[25] = segNo;
                segNo++;
                BitConverter.GetBytes(source.Timestamp.Add(source.TTL).ToBinary()).CopyTo(seg, 26);
                BitConverter.GetBytes(segLength).CopyTo(seg, 34);

                byte[] segData = new byte[seg.Length + sdata.Length];
                seg.CopyTo(segData, 0);
                sdata.CopyTo(segData, 36);

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
        public ulong Sender { get; private set; }
        public Guid MessageId { get; private set; }

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
            if (segment.MessageId == Guid.Empty) return;
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
    }
}
