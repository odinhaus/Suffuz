using Altus.Suffūz.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Altus.Suffūz.IO;
using Altus.Suffūz.Serialization.Binary;

namespace Altus.Suffūz.Observables.Serialization.Binary
{
    public class ChangeStateSerializer : ISerializer<ChangeState>
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
                return 0;
            }
        }

        public ChangeState Deserialize(Stream inputSource)
        {
            ChangeState value;
            using (var br = new BinaryReader(inputSource))
            {
                Type valueType = TypeHelper.GetType(br.ReadString());
                Type changeType = typeof(Change<>).MakeGenericType(valueType);
                value = (ChangeState)Activator.CreateInstance(typeof(ChangeState<>).MakeGenericType(valueType));
                value.ObservableId = br.ReadString();
                value.Epoch = br.ReadUInt64();
                value.PropertyName = br.ReadString();

                ISerializer changeSerializer = App.Resolve<ISerializationContext>().GetSerializer(changeType, StandardFormats.BINARY);
                var count = br.ReadInt32();

                for (int i = 0; i < count; i++)
                {
                    var bytes = br.ReadBytes(br.ReadInt32());
                    value.Add(changeSerializer.Deserialize(bytes, changeType));
                }
            }

            return value;
        }

        public ChangeState Deserialize(byte[] source)
        {
            using (var ms = new MemoryStream(source))
            {
                return Deserialize(ms);
            }
        }

        public object Deserialize(byte[] source, Type targetType)
        {
            return Deserialize(source);
        }

        public byte[] Serialize(object source)
        {
            return Serialize((ChangeState)source);
        }

        public byte[] Serialize(ChangeState source)
        {
            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    bw.Write(source.GetType().GetGenericArguments()[0].AssemblyQualifiedName);
                    bw.Write(source.ObservableId);
                    bw.Write(source.Epoch);
                    bw.Write(source.PropertyName);
                    bw.Write(source.Count);

                    ISerializer changeSerializer = App.Resolve<ISerializationContext>().GetSerializer(typeof(Change<>).MakeGenericType(source.ValueType), StandardFormats.BINARY);
                    
                    foreach(var change in source)
                    {
                        var bytes = changeSerializer.Serialize(change);
                        bw.Write(bytes.Length);
                        bw.Write(bytes);
                    }


                    ms.Seek(0, SeekOrigin.Begin);
                    return ms.ToArray();
                }
            }
        }

        public void Serialize(ChangeState source, Stream outputStream)
        {
            StreamHelper.Write(outputStream, Serialize(source));
        }

        public bool SupportsFormat(string format)
        {
            return format == StandardFormats.BINARY;
        }

        public bool SupportsType(Type type)
        {
            return type.Equals(typeof(ChangeState)) 
                || (type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(ChangeState<>)));
        }
    }
}
