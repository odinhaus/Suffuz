using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Serialization
{
    public interface IDictionarySerializable
    {
        Dictionary<string, object> ToDictionary();
        void FromDictionary(Dictionary<string, object> values);
    }
}
