using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Messages
{
    public class NominateResponse
    {
        [BinarySerializable(0)]
        public double Score { get; set; }
    }
}
