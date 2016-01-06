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
        public ushort IdentityId { get; set; }
        public ulong Version { get; set; }
        public string Key { get; set; }
        public object Value { get; set; }
    }
}
