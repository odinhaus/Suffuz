using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization;
using Altus.Suffūz.Serialization;
using Altus.Suffūz.IO;
using Altus.Suffūz.Serialization.Binary;

using Altus.Suffūz.Protocols;
using Altus.Suffūz.Test;
using Altus.Suffūz.Tests;
using Altus.Suffūz.IO;

namespace Altus.Suffūz.Protocols
{
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Suffūz", "1.0")]
    [System.Serializable]
    public class BinarySerializer_RoutablePayload : Altus.Suffūz.Protocols.RoutablePayload, ISerializer<Altus.Suffūz.Protocols.RoutablePayload>
    {
        byte[] _bytes = new byte[0];
        protected byte[] OnSerialize(object source)
        {
            var typed = (SimplePOCO)source;
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter br = new BinaryWriter(ms);


                return ms.ToArray();
            }
        }

        protected object OnDeserialize(byte[] source, Type targetType)
        {
            using (MemoryStream ms = new MemoryStream(source))
            {
                BinaryReader br = new BinaryReader(ms);
                var typed = new SimplePOCO();


                return typed;
            }
        }

        public int Priority
        {
            get;
            private set;
        }

        public bool IsScalar { get { return false; } }

        public bool SupportsFormat(string format)
        {
            return format.Equals(StandardFormats.BINARY, StringComparison.InvariantCultureIgnoreCase);
        }

        public bool SupportsType(Type type)
        {
            return type == this.GetType().BaseType 
                || typeof(ISerializer<Altus.Suffūz.Protocols.RoutablePayload>).IsAssignableFrom(type);
        }

        public byte[] Serialize(object source)
        {
            return OnSerialize(source);
        }

        public object Deserialize(byte[] source, Type targetType)
        {
            return OnDeserialize(source, targetType);
        }

        public byte[] Serialize(Altus.Suffūz.Protocols.RoutablePayload source)
        {
            return this.OnSerialize(source);
        }

        public void Serialize(Altus.Suffūz.Protocols.RoutablePayload source, System.IO.Stream outputStream)
        {
            StreamHelper.Copy(Serialize(source), outputStream);
        }

        public Altus.Suffūz.Protocols.RoutablePayload Deserialize(byte[] source)
        {
            return (Altus.Suffūz.Protocols.RoutablePayload)this.OnDeserialize(source, typeof(Altus.Suffūz.Protocols.RoutablePayload));
        }

        public Altus.Suffūz.Protocols.RoutablePayload Deserialize(System.IO.Stream inputSource)
        {
            return Deserialize(StreamHelper.GetBytes(inputSource));
        }
    }

    public class Testing
    {
        protected byte[] OnSeserialize(object obj1)
        {
            var time = (ComplexPOCO)obj1;
            using (MemoryStream stream = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(stream);
                var a = time.SimplePOCO;
                bool hasValue = a != null;
                writer.Write(hasValue);
                if (hasValue)
                {
                    _BinarySerializer.Serialize(typeof(SimplePOCO), a, writer);
                }
                return stream.ToArray();
            }
        }

        protected object OnDeserialize(byte[] buffer1, Type type)
        {
            ComplexPOCO serializer;
            using (MemoryStream stream = new MemoryStream(buffer1))
            {
                BinaryReader reader = new BinaryReader(stream);
                serializer = new ComplexPOCO();
                if (reader.BaseStream.Position >= reader.BaseStream.Length)
                {
                    return serializer;
                }
                if (reader.ReadBoolean())
                {
                    serializer.SimplePOCO = (SimplePOCO)_BinarySerializer.Deserialize(typeof(SimplePOCO), reader);
                }
            }
            return serializer;
        }


    }
}