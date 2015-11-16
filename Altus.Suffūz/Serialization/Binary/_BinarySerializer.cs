using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Serialization.Binary
{
    public static class _BinarySerializer
    {
        static Dictionary<Type, ISerializer> _serializers = new Dictionary<Type, ISerializer>();

        public static void Serialize(object source, BinaryWriter bw)
        {
            if (source == null)
            {
                bw.Write(SerializationContext.Instance.TextEncoding.GetBytes("<null>"));
            }
            else
            {
                Type t = source.GetType();
                string tname = t.AssemblyQualifiedName;
                if (typeof(ISerializer).IsAssignableFrom(t))
                {
                    Type baseType = t.BaseType;
                    Type serializerGen = typeof(ISerializer<>);
                    Type serializerSpec = serializerGen.MakeGenericType(baseType);
                    if (serializerSpec.IsAssignableFrom(t))
                    {
                        tname = baseType.AssemblyQualifiedName;
                    }
                }

                ISerializer serializer = null;
                lock (_serializers)
                {
                    try
                    {
                        serializer = _serializers[t];
                    }
                    catch
                    {
                        serializer = App.Resolve<ISerializationContext>().GetSerializer(t, StandardFormats.BINARY);
                        try
                        {
                            _serializers.Add(t, serializer);
                        }
                        catch { }
                    }
                }
                if (serializer == null) throw (new System.Runtime.Serialization.SerializationException("Serializer not found for type \"" + tname + "\" supporting the " + StandardFormats.BINARY + " format."));
                if (t.IsArray)
                {
                    bw.Write(((Array)source).Length);
                    foreach (object item in (Array)source)
                    {
                        byte[] data = serializer.Serialize(source);
                        bw.Write(tname);
                        bw.Write(data.Length);
                        bw.Write(data);
                    }
                }
                else
                {
                    byte[] data = serializer.Serialize(source);
                    bw.Write(tname);
                    bw.Write(data.Length);
                    bw.Write(data);
                }
            }
        }

        public static object Deserialize(BinaryReader br)
        {
            string tname = br.ReadString();
            if (tname.Equals("<null>", StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }
            else
            {
                Type t = TypeHelper.GetType(tname);
                if (t == null)
                    throw (new System.Runtime.Serialization.SerializationException("Type not found: " + tname));
                ISerializer serializer = null;
                lock (_serializers)
                {
                    try
                    {
                        serializer = _serializers[t];
                    }
                    catch
                    {
                        serializer = App.Resolve<ISerializationContext>().GetSerializer(t, StandardFormats.BINARY);
                        try
                        {
                            _serializers.Add(t, serializer);
                        }
                        catch { }
                    }
                }
                if (serializer == null) throw (new System.Runtime.Serialization.SerializationException("Serializer not found for type \"" + tname + "\" supporting the " + StandardFormats.BINARY + " format."));

                if (t.IsArray)
                {
                    int count = br.ReadInt32();
                    Array list = (Array)Activator.CreateInstance(t, count);

                    for (int i = 0; i < count; i++)
                    {
                        list.SetValue(serializer.Deserialize(br.ReadBytes(br.ReadInt32()), t), i);
                    }

                    return list;
                }
                else
                {
                    return serializer.Deserialize(br.ReadBytes(br.ReadInt32()), t);
                }
            }
        }
    }
}
