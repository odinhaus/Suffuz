using Altus.Suffūz.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Altus.Suffūz.Protocols.Udp
{
    public class UdpMessageSerializer : ISerializer<UdpMessage>
    {
        public bool IsScalar
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public int Priority
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public UdpMessage Deserialize(Stream inputSource)
        {
            throw new NotImplementedException();
        }

        public UdpMessage Deserialize(byte[] source)
        {
            throw new NotImplementedException();
        }

        public object Deserialize(byte[] source, Type targetType)
        {
            throw new NotImplementedException();
        }

        public byte[] Serialize(object source)
        {
            throw new NotImplementedException();
        }

        public byte[] Serialize(UdpMessage source)
        {
            throw new NotImplementedException();
        }

        public void Serialize(UdpMessage source, Stream outputStream)
        {
            throw new NotImplementedException();
        }

        public bool SupportsFormat(string format)
        {
            throw new NotImplementedException();
        }

        public bool SupportsType(Type type)
        {
            throw new NotImplementedException();
        }
    }
}
