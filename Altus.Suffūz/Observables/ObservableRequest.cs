using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public class ObservableRequest
    {
        [BinarySerializable(0)]
        public string GlobalKey { get; set; }
    }
}
