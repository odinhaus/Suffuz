using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion.Serialization
{
    public interface ISerializationContext
    {
        ISerializer<T> GetSerializer<T>(string format);
        ISerializer GetSerializer(Type type, string format);
        Encoding TextEncoding { get; set; }
    }
}
