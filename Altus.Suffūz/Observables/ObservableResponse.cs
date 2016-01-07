using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public class ObservableResponse
    {
        [BinarySerializable(0)]
        public string GlobalKey { get; set; }
        [BinarySerializable(1)]
        public VersionVectorInstance Vector { get; set; }
    }
}
