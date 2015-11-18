using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Tests
{
    public class ComplexPOCO
    {
        [BinarySerializable(0)]
        public SimplePOCO SimplePOCO { get; set; }
    }
}
