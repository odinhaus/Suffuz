using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public class Change<T>
    {
        [BinarySerializable(0)]
        public T BaseValue { get; set; }
        [BinarySerializable(1)]
        public T NewValue { get; set; }
        [BinarySerializable(2)]
        public DateTime Timestamp { get; set; }

        public override bool Equals(object obj)
        {
            return obj is Change<T>
                && ((Change<T>)obj).BaseValue.Equals(BaseValue)
                && ((Change<T>)obj).NewValue.Equals(NewValue)
                && ((Change<T>)obj).Timestamp == Timestamp;
        }
    }
}
