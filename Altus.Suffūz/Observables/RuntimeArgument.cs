using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public class RuntimeArgument
    {
        public RuntimeArgument(string name, object value)
        {
            Name = name;
            Value = value;
        }
        public string Name { get; private set; }
        public object Value { get; private set; }
    }
}
