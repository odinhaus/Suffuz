using System;
using System.Collections;
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

        public static void Serialize(Type targetType, object source, BinaryWriter bw)
        {
            bw.Write(source != null);
            if (source != null)
            {
                var type = targetType;
                if (targetType == typeof(object) && source.GetType() != typeof(object))
                {
                    type = source.GetType();
                }

                string tname = type.AssemblyQualifiedName;
                if (typeof(ISerializer).IsAssignableFrom(targetType))
                {
                    Type baseType = type.BaseType;
                    Type serializerGen = typeof(ISerializer<>);
                    Type serializerSpec = serializerGen.MakeGenericType(baseType);
                    if (serializerSpec.IsAssignableFrom(type))
                    {
                        tname = baseType.AssemblyQualifiedName;
                    }
                }

                ISerializer serializer = null;
                lock (_serializers)
                {
                    try
                    {
                        serializer = _serializers[type];
                    }
                    catch
                    {
                        try
                        {
                            serializer = App.Resolve<ISerializationContext>().GetSerializer(type, StandardFormats.BINARY);
                        }
                        catch
                        {
                            try
                            {
                                serializer = new ILSerializerBuilder().CreateSerializerType(type);
                            }
                            catch { }
                        }
                        try
                        {
                            _serializers.Add(type, serializer);
                        }
                        catch { }
                    }
                }

                if (serializer == null)
                    throw (new System.Runtime.Serialization.SerializationException("Serializer not found for type \"" + tname + "\" supporting the " + StandardFormats.BINARY + " format."));

                byte[] data = serializer.Serialize(source);
                if (targetType == source.GetType())
                {
                    bw.Write(true);
                }
                else
                {
                    bw.Write(false);
                    bw.Write(tname);
                }
                bw.Write(data.Length);
                bw.Write(data);
                
            }
        }

        public static object Deserialize(Type targetType, BinaryReader br)
        {
            var isNotNull = br.ReadBoolean();
            if (!isNotNull) return null;

            var isTargetType = br.ReadBoolean();
            string tname = null;
            Type t = targetType;

            if (!isTargetType)
            {
                tname = br.ReadString();
                t = TypeHelper.GetType(tname);
            }

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
                    try
                    {
                        serializer = App.Resolve<ISerializationContext>().GetSerializer(t, StandardFormats.BINARY);
                    }
                    catch
                    {
                        try
                        {
                            serializer = new ILSerializerBuilder().CreateSerializerType(t);
                        }
                        catch { }
                    }
                    try
                    {
                        _serializers.Add(t, serializer);
                    }
                    catch { }
                }
            }
            if (serializer == null) throw (new System.Runtime.Serialization.SerializationException("Serializer not found for type \"" + tname + "\" supporting the " + StandardFormats.BINARY + " format."));

                
            return serializer.Deserialize(br.ReadBytes(br.ReadInt32()), t);
            
        }
    }
}
