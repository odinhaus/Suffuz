using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Serialization.Binary
{
    public class IListSerializer : ISerializer
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
                        var elemType = GetElementType(listType);
                        if (listType == typeof(IEnumerable)
                            || (listType.IsGenericType && listType.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
                        {
                            listType = typeof(List<>).MakeGenericType(elemType);
                        }

                        var list = (IList)Activator.CreateInstance(listType);

                        for(int i = 0; i < count; i++)
                        {
                            list.Add(_BinarySerializer.Deserialize(elemType, br));
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
            var list = source as IList;
            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    bw.Write(source != null);
                    if (source != null)
                    {
                        Type elemType = GetElementType(list);
                        bw.Write(source.GetType().AssemblyQualifiedName);
                        bw.Write(list.Count);
                        for (int i = 0; i < list.Count; i++)
                        {
                            // unfortunately, lists can have mixed types
                            var item = list[i];
                            _BinarySerializer.Serialize(elemType, item, bw);
                        }
                    }
                }

                return ms.ToArray();
            }
        }

        private Type GetElementType(IList list)
        {
            var listType = list.GetType();
            var type = GetElementType(listType);
            if (type == typeof(object) && list.Count > 0)
            {
                type = list[0].GetType();
            }
            return type;
        }

        private Type GetElementType(Type listType)
        {
            if (listType.IsArray)
            {
                return listType.GetElementType();
            }
            else if(listType.Implements(typeof(IEnumerable<>)))
            {
                return listType.GetGenericArguments()[0];
            }
            else return typeof(object);
        }

        public bool SupportsFormat(string format)
        {
            return format.Equals(StandardFormats.BINARY, StringComparison.InvariantCultureIgnoreCase);
        }

        public bool SupportsType(Type type)
        {
            return IsListType(type);
        }

        public static bool IsListType(Type type)
        {
            return type.Implements(typeof(IList));
        }
    }
}
