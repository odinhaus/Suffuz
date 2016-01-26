using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Serialization
{
    public interface ICustomConstructor
    {
        object[] GetCtorArgs();
    }
}
