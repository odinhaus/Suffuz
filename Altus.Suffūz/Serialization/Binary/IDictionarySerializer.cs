using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altus.Suffūz.Collections.Linq;

namespace Altus.Suffūz.Serialization.Binary
{
    public class IDictionarySerializer : ISerializer
    {
        public int Priority { get; private set; }
        public bool IsScalar { get { return false; } }

        public object Deserialize(byte[] source, Type targetType)
        {
            using (var ms = new MemoryStream(source))
            {
                using (var br = new BinaryReader(ms))
                {
                    var isNotNull = br.ReadBoolean();
                    if (isNotNull)
                    {
                        var listTypeName = br.ReadString();
                        var listType = TypeHelper.GetType(listTypeName);
                        var count = br.ReadInt32();
                        var keyType = GetKeyType(listType);
                        var elemType = GetElementType(listType);
                        
                        var list = (IDictionary)Activator.CreateInstance(listType);

                        for (int i = 0; i < count; i++)
                        {
                            list.Add(_BinarySerializer.Deserialize(keyType, br), _BinarySerializer.Deserialize(elemType, br));
                        }

                        return list;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        public byte[] Serialize(object source)
        {
            var list = source as IDictionary;
            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    bw.Write(source != null);
                    if (source != null)
                    {
                        Type elemType, keyType;
                        GetDictionaryTypes(list, out keyType, out elemType);
                        bw.Write(source.GetType().AssemblyQualifiedName);
                        bw.Write(list.Count);
                        var en = list.GetEnumerator();
                        while(en.MoveNext())
                        {
                            _BinarySerializer.Serialize(keyType, en.Key, bw);
                            _BinarySerializer.Serialize(elemType, en.Value, bw);
                        }
                    }
                }

                return ms.ToArray();
            }
        }

        private void GetDictionaryTypes(IDictionary list, out Type keyType, out Type elemType)
        {
            var listType = list.GetType();
            keyType = GetKeyType(listType);
            if (keyType == typeof(object) && list.Count > 0)
            {
                keyType = list.Keys.First().GetType();
            }
            elemType = GetElementType(listType);
            if (elemType == typeof(object) && list.Count > 0)
            {
                elemType = list.Values.First()?.GetType() ?? typeof(object);
            }
        }



        private Type GetKeyType(Type listType)
        {
            if(listType.Implements(typeof(IDictionary<,>)))
            {
                return listType.GetGenericArguments()[0];
            }
            else return typeof(object);
        }

        private Type GetElementType(Type listType)
        {
            if (listType.Implements(typeof(IDictionary<,>)))
            {
                return listType.GetGenericArguments()[1];
            }
            else return typeof(object);
        }

        public bool SupportsFormat(string format)
        {
            return format.Equals(StandardFormats.BINARY, StringComparison.InvariantCultureIgnoreCase);
        }

        public bool SupportsType(Type type)
        {
            return IsDictionaryType(type);
        }

        public static bool IsDictionaryType(Type type)
        {
            return type.Implements(typeof(IDictionary));
        }
    }
}
