using Altus.Suffūz.Protocols.Udp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Protocols
{
    public abstract class MessageSegment
    {
        /*
        * FIELD                        LENGTH              POS     SUBFIELDS/Description
        * TAG                          1                   0       Segment Type (0 = Header, 1 = Segment)
        * SENDERID + MESSAGEID         8                   1       Combination of SENDER (16 bits) + MESSAGE SEQUENCE NUMBER (48 bits) = 64 bits
        * SEGMENTID                    8                   9       Unique incremental ulong per packet
        */

        public MessageSegment(IChannel connection, Protocol protocol, EndPoint ep, byte[] data)
        {
            Protocol = protocol;
            EndPoint = ep;
            Data = data;
            Connection = connection;
        }

        public abstract byte[] Payload { get; }
        public abstract ushort PayloadLength { get; }
        public abstract ushort SegmentNumber { get; }
        public abstract ushort SegmentCount { get; }
        public abstract int SegmentLength { get; }

        public abstract TimeSpan TimeToLive { get; }
        public byte[] Data { get; set; }
        public Protocol Protocol { get; set; }
        public EndPoint EndPoint { get; set; }
        public IChannel Connection { get; private set; }
        public bool IsValid { get { return OnIsValid(); } }

        protected abstract bool OnIsValid();

        private ushort _sender;
        public unsafe ushort Sender
        {
            get
            {
                if (_sender == 0 && Data != null)
                {
                    fixed (byte* Pointer = Data)
                    {
                        _sender = (ushort)(*(((ulong*)(Pointer + 1))) >> 48);
                    }
                }
                return _sender;
            }
        }

        private ulong _segmentId;
        public unsafe ulong SegmentId
        {
            get
            {
                if (_segmentId == 0 && Data != null)
                {
                    fixed (byte* Pointer = Data)
                    {
                        _segmentId = *(((ulong*)(Pointer + 9)));
                    }
                }
                return _segmentId;
            }
        }

        private ulong _id = 0;
        public unsafe ulong MessageId
        {
            get
            {
                if (_id == 0 && Data != null)
                {
                    fixed (byte* Pointer = Data)
                    {
                        _id = *(((ulong*)(Pointer + 1)));
                    }
                }
                return _id;
            }
        }

        private SegmentType _type = SegmentType.Unknown;
        public SegmentType SegmentType
        {
            get
            {
                if (_type == SegmentType.Unknown
                    && Data != null)
                {
                    if (((int)Data[0] & (1 << 2)) == (1 << 2))
                    {
                        // segment NAK
                        _type = SegmentType.NAK;
                    }
                    else if(((int)Data[0] & (1 << 1)) == (1 << 1))
                    {
                        // segment
                        _type = SegmentType.Segment;
                    }
                    else
                    {
                        // header
                        _type = SegmentType.Header;
                    }
                }
                return _type;
            }
        }




        internal static bool TryCreate(IChannel connection, Protocol protocol, EndPoint ep, byte[] buffer, out MessageSegment segment)
        {
            segment = null;
            switch (protocol)
            {
                case Protocol.Udp:
                    {
                        if (((int)buffer[0] & (1 << 2)) == (1 << 2))
                        {
                            // segment NAK
                            segment = new UdpSegmentNAK(connection, ep, buffer);
                        }
                        else if (((int)buffer[0] & (1 << 1)) == (1 << 1))
                        {
                            // segment
                            segment = new UdpSegment(connection, ep, buffer);
                        }
                        else if (buffer[0] == 0)
                        {
                            // header
                            segment = new UdpHeader(connection, ep, buffer);
                        }
                        break;
                    }
                //case Protocol.Tcp:
                //    {
                //        if (((int)buffer[0] & (1 << 1)) == (1 << 1))
                //        {
                //            // segment
                //            segment = new TcpSegment(connection, ep, buffer);
                //        }
                //        else
                //        {
                //            // header
                //            segment = new TcpHeader(connection, ep, buffer);
                //        }
                //        break;
                //    }
                default:
                    {
                        throw (new NotImplementedException());
                    }
            }
            if (!segment.IsValid) segment = null;
            return segment != null;
        }
    }
}
