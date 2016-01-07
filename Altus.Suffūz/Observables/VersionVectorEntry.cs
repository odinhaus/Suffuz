using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public class VersionVectorEntry<T> : VersionVectorEntry
    {
        public new T Value
        {
            get { return (T)base.Value; }
            set { base.Value = value; }
        }
    }

    public class VersionVectorEntry
    {
        [BinarySerializable(0)]
        public ushort IdentityId { get; set; }
        [BinarySerializable(1)]
        public ulong Version { get; set; }
        [BinarySerializable(2)]
        public string Key { get; set; }
        [BinarySerializable(3)]
        public object Value { get; set; }
    }
}
