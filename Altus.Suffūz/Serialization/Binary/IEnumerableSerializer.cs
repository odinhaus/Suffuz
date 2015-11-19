using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Serialization.Binary
{
    public class IEnumerableSerializer : ISerializer
    {
        static Dictionary<Type, Delegate> _casters = new Dictionary<Type, Delegate>();

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
                        var enumerableTypeName = br.ReadString();
                        var enumerableType = TypeHelper.GetType(enumerableTypeName);
                        var count = br.ReadInt32();
                        var elemType = GetElementType(targetType);
                        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elemType));

                        for(int i = 0; i < count; i++)
                        {
                            list.Add(_BinarySerializer.Deserialize(elemType, br));
                        }

                        Delegate caster;
                        lock(_casters)
                        {
                            
                            if (!_casters.TryGetValue(enumerableType, out caster))
                            {
                                caster = Cast(enumerableType, elemType);
                            }
                        }
                        
                        return caster.DynamicInvoke(list);
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        private Delegate Cast(Type enumerableType, Type elementType)
        {
            var p = Expression.Parameter(typeof(IList<>).MakeGenericType(elementType));
            var call = Expression.Call(null, typeof(Enumerable).GetMethod("AsEnumerable").MakeGenericMethod(elementType), p);
            var cast = Expression.Convert(call, enumerableType);
            return Expression.Lambda(cast, p).Compile();
        }

        public byte[] Serialize(object source)
        {
            var enumerable = source as IEnumerable;
            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    if (source == null)
                    {
                        bw.Write(false);
                    }
                    else
                    {
                        var en = enumerable.GetEnumerator();
                        en.MoveNext();
                        var item0 = en.Current;
                        var elemType = item0.GetType();
                        var listType = typeof(List<>).MakeGenericType(elemType);
                        var list = (IList)Activator.CreateInstance(listType);
                        list.Add(item0);
                        while (en.MoveNext())
                            list.Add(en.Current);

                        var count = list.Count;
                        var enumerableType = typeof(IEnumerable<>).MakeGenericType(elemType).AssemblyQualifiedName;
                        bw.Write(true);
                        bw.Write(enumerableType);
                        bw.Write(count);

                        for(int i = 0; i < count; i++)
                        {
                            _BinarySerializer.Serialize(elemType, list[i], bw);
                        }

                    }
                }

                return ms.ToArray();
            }
        }

        private Type GetElementType(Type listType)
        {
            if (listType.Implements(typeof(IEnumerable<>)))
            {
                return listType.GetGenericArguments()[0];
            }
            else if (listType.IsArray)
            {
                return listType.GetElementType();
            }
            else return typeof(object);
        }

        public bool SupportsFormat(string format)
        {
            return format.Equals(StandardFormats.BINARY, StringComparison.InvariantCultureIgnoreCase);
        }

        public bool SupportsType(Type type)
        {
            return IsIEnumerableType(type);
        }

        public static bool IsIEnumerableType(Type type)
        {
            return type.Implements(typeof(IEnumerable)) && type.IsInterface;
        }
    }
}
