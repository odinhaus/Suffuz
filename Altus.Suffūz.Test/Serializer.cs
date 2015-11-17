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

namespace Altus.Suffūz.Protocols
{
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Suffūz", "1.0")]
    [System.Serializable]
    public class BinarySerializer_RoutablePayload : Altus.Suffūz.Protocols.RoutablePayload, ISerializer<Altus.Suffūz.Protocols.RoutablePayload>
    {
        protected byte[] OnSerialize(object source)
        {
            RoutablePayload typed = (RoutablePayload)source;
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter br = new BinaryWriter(ms);
                this.SerializeType(typed.Payload, br);

                br.Write(typed.PayloadType == null ? "" : typed.PayloadType);
                br.Write(typed.ReturnType == null ? "" : typed.ReturnType);

                return ms.ToArray();
            }
        }

        protected object OnDeserialize(byte[] source, Type targetType)
        {
            using (MemoryStream ms = new MemoryStream(source))
            {
                BinaryReader br = new BinaryReader(ms);
                BinarySerializer_RoutablePayload typed = new BinarySerializer_RoutablePayload();
                if (br.BaseStream.Position >= br.BaseStream.Length) return typed;
                typed.Payload = (System.Object)this.DeserializeType(br);
                if (br.BaseStream.Position >= br.BaseStream.Length) return typed;
                typed.PayloadType = br.ReadString();
                if (br.BaseStream.Position >= br.BaseStream.Length) return typed;
                typed.ReturnType = br.ReadString();

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

        protected object DeserializeType(BinaryReader br)
        {
            return _BinarySerializer.Deserialize(br);
        }

        protected void SerializeType(object source, BinaryWriter br)
        {
            _BinarySerializer.Serialize(source, br);
        }
    }
}