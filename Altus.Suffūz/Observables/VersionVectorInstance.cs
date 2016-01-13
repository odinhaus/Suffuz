using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public class VersionVectorInstance<T> : VersionVectorInstance
    {
        public new T Value
        {
            get { return (T)base.Value; }
            set { base.Value = value; }
        }
    }

    public class VersionVectorInstance : VersionVectorEntry
    {
        public VersionVectorInstance()
        {
            MemberVectors = new VersionVector<object>();
        }
        [BinarySerializable(4)]
        public VersionVector<object> MemberVectors { get; set; }
    }
}
