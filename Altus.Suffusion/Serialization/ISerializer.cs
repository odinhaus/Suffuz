using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion.Serialization
{
    public interface ISerializer
    {
        bool SupportsFormat(string format);
        bool SupportsType(Type type);
        byte[] Serialize(object source);
        object Deserialize(byte[] source, Type targetType);
        int Priority { get; }
        bool IsScalar { get; }
    }

    public interface ISerializer<T> : ISerializer
    {
        byte[] Serialize(T source);
        void Serialize(T source, Stream outputStream);
        T Deserialize(byte[] source);
        T Deserialize(Stream inputSource);
    }
}
