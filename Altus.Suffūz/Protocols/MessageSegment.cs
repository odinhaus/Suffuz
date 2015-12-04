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
        public MessageSegment(IChannel connection, Protocol protocol, EndPoint ep, byte[] data)
        {
            Protocol = protocol;
            EndPoint = ep;
            Data = data;
            Connection = connection;
        }

        public abstract byte[] Payload { get; }
        public abstract ushort PayloadLength { get; }
        public abstract uint SegmentNumber { get; }
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
                        _sender = *(((ushort*)(Pointer + 1 + 6)));
                    }
                }
                return _sender;
            }
        }

        private ulong _sequenceNumber;
        public unsafe ulong SequenceNumber
        {
            get
            {
                if (_sequenceNumber == 0 && Data != null)
                {
                    fixed (byte* Pointer = Data)
                    {
                        _sequenceNumber = *(((ulong*)(Pointer + 1)));
                    }
                    _sequenceNumber = ((_sequenceNumber << 16) >> 16);
                }
                return _sequenceNumber;
            }
        }

        private ulong _id = 0;
        public ulong MessageId
        {
            get
            {
                if (_id == 0)
                {
                    _id = ((ulong)Sender << 48) + SequenceNumber;
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
                    if (((int)Data[0] & (1 << 1)) == (1 << 1))
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
                        if (((int)buffer[0] & (1 << 1)) == (1 << 1))
                        {
                            // segment
                            segment = new UdpSegment(connection, ep, buffer);
                        }
                        else
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
