using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Tests
{
    public class ComplexPOCO
    {
        [BinarySerializable(0)]
        public SimplePOCO SimplePOCO { get; set; }

        [BinarySerializable(1)]
        public List<int> ListOfInt { get; set; }

        [BinarySerializable(2, SerializationType = typeof(SimplePOCO[]))]
        public IEnumerable<SimplePOCO> IEnumerableOfSimplePOCO { get; set; }

        [BinarySerializable(3)]
        public ObservableCollection<SimplePOCO> CollectionOfSimplePOCO { get; set; }
        [BinarySerializable(4)]
        public Dictionary<int, SimplePOCO> DictionaryOfSimplePOCO { get; set; }
    }
}
