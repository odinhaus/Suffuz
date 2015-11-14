using Altus.Suffusion.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion.Protocols
{
    public class RoutablePayload
    {
        public RoutablePayload()
        { }
        public RoutablePayload(object payload, Type payloadType, Type returnType)
        {
            Payload = payload;
            PayloadType = payloadType.AssemblyQualifiedName;
            ReturnType = returnType.AssemblyQualifiedName;
        }
        [BinarySerializable(0)]
        public object Payload { get; set; }
        [BinarySerializable(1)]
        public string PayloadType { get; set; }
        [BinarySerializable(2)]
        public string ReturnType { get; set; }
    }
}
