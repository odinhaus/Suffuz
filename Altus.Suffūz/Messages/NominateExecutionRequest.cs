using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Messages
{
    public class NominateExecutionRequest
    {
        [BinarySerializable(0)]
        public string Nominator { get; set; }
        [BinarySerializable(1)]
        public object Request { get; set; }
        [BinarySerializable(2)]
        public bool ScalarResults { get; set; }
    }
}
