using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion.Serialization.Binary
{
    public class ComplexSerializer : ISerializer
    {
        public bool IsScalar { get { return false; } }
        public ComplexSerializer(IBinarySerializerBuilder builder)
        {
            Priority = int.MaxValue;
            Builder = builder;
        }

        public int Priority { get; private set; }

        private IBinarySerializerBuilder Builder;

        public bool SupportsFormat(string format)
        {
            return format.Equals(StandardFormats.BINARY, StringComparison.InvariantCultureIgnoreCase);
        }

        public bool SupportsType(Type type)
        {
            return !PrimitiveSerializer.IsPrimitive(type)
                && type != typeof(string);
        }
        static Dictionary<Type, ISerializer> _serializers = new Dictionary<Type, ISerializer>();
        public byte[] Serialize(object source)
        {
            if (Builder == null) throw (new SerializationException("Binary serializer builder could not be found."));

            ISerializer serializer = null;
            Type t = source.GetType();
            try
            {
                serializer = _serializers[t];
            }
            catch
            {
                serializer = Builder.CreateSerializerType(source.GetType());
                try
                {
                    _serializers.Add(t, serializer);
                }
                catch { }
            }

            if (serializer == null) throw (new SerializationException("Serializer for type\"" + source.GetType().FullName + "\" could not be found supporting the " + StandardFormats.BINARY + " format."));

            return serializer.Serialize(source);
        }

        public object Deserialize(byte[] source, Type targetType)
        {
            ISerializer serializer = Builder.CreateSerializerType(targetType);
            if (serializer == null) throw (new SerializationException("Serializer for type\"" + targetType.FullName + "\" could not be found supporting the " + StandardFormats.BINARY + " format."));

            return serializer.Deserialize(source, targetType);
        }
    }
}
