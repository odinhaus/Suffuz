using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Serialization
{
    public interface ISerializationContext
    {
        ISerializer<T> GetSerializer<T>(string format);
        ISerializer GetSerializer(Type type, string format);
        void SetSerializer<TObject, TSerializer>(string format) where TSerializer : ISerializer<TObject>, new();
        void SetSerializer(Type objectType, Type serializerType, string format);
        Encoding TextEncoding { get; set; }
    }
}
