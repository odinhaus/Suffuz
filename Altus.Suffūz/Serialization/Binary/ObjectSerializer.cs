using Altus.Suffūz.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Serialization.Binary
{
    public class ObjectSerializer : ISerializer
    {
        public int Priority { get; private set; }
        public bool IsScalar { get { return true; } }
        public static bool IsObject(Type t)
        {
            return t == typeof(object);
        }

        public byte[] Serialize(object source)
        {
            if (source == null)
                return new byte[] { 0 };
            return new byte[] { 1 };
        }


        public object Deserialize(byte[] source, Type targetType)
        {
            if (source == null || source.Length == 0 || source[0] == 0)
            {
                return null;
            }
            else
            {
                return new object();
            }
        }

        public bool SupportsFormat(string format)
        {
            return format.Equals(StandardFormats.BINARY, StringComparison.InvariantCultureIgnoreCase);
        }

        public virtual bool SupportsType(Type type)
        {
            return IsObject(type);
        }
    }
}
