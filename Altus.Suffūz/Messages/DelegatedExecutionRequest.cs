using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Messages
{
    public class DelegatedExecutionRequest
    {
        [BinarySerializable(0)]
        public string Delegator { get; set; }
        [BinarySerializable(1)]
        public object Request { get; set; }
    }
}
