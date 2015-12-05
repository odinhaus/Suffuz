using Altus.Suffūz.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Altus.Suffūz.IO;

namespace Altus.Suffūz.Protocols.Udp
{
    public class UdpMessageSerializer : ISerializer<UdpMessage>
    {
        public bool IsScalar
        {
            get
            {
                return false;
            }
        }

        public int Priority
        {
            get
            {
                return int.MaxValue;
            }
        }

        public UdpMessage Deserialize(Stream inputSource)
        {
            return Deserialize(inputSource.GetBytes());
        }

        public UdpMessage Deserialize(byte[] source)
        {
            return new UdpMessage(source);
        }

        public object Deserialize(byte[] source, Type targetType)
        {
            return Deserialize(source);
        }

        public byte[] Serialize(object source)
        {
            return Serialize((UdpMessage)source);
        }

        public byte[] Serialize(UdpMessage source)
        {
            return source.ToBytes();
        }

        public void Serialize(UdpMessage source, Stream outputStream)
        {
            outputStream.Write(Serialize(source));
        }

        public bool SupportsFormat(string format)
        {
            return format.Equals(StandardFormats.BINARY);
        }

        public bool SupportsType(Type type)
        {
            return type == typeof(UdpMessage);
        }
    }
}
