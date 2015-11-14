using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Serialization.Binary
{
    public interface IBinarySerializerBuilder
    {
        ISerializer CreateSerializerType(Type type);
        ISerializer<T> CreateSerializerType<T>();
    }
}
