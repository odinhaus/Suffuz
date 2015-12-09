using Altus.Suffūz.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Protocols
{
    public class MessageSegmentSerializer : ISerializer
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

        public object Deserialize(byte[] source, Type targetType)
        {
            try
            {
                var ctor = targetType.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new Type[] { typeof(byte[]) },
                    null);
                return ctor.Invoke(new object[] { source });
            }
            catch(Exception ex)
            {
                throw;
            }
        }

        public byte[] Serialize(object source)
        {
            return ((MessageSegment)source).Data.Take(((MessageSegment)source).SegmentLength).ToArray();
        }

        public bool SupportsFormat(string format)
        {
            return StandardFormats.BINARY == format;
        }

        public bool SupportsType(Type type)
        {
            return type.IsTypeOrSubtypeOf<MessageSegment>();
        }
    }
}
