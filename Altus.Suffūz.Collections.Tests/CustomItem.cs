using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Collections.Tests
{
    public class CustomItem
    {
        [BinarySerializable(0)]
        public int A { get; set; }
        [BinarySerializable(1)]
        public string B { get; set; }
    }
}
