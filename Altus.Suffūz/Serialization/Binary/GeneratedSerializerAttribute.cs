using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Serialization.Binary
{
    public class GeneratedSerializerAttribute : Attribute
    {
        public GeneratedSerializerAttribute(Type wrappedType)
        {
            this.WrappedType = wrappedType;
        }

        public Type WrappedType { get; private set; }
    }
}
