using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public abstract class ConflictingEventAttribute : Attribute
    {
        protected ConflictingEventAttribute() { }

        public Type ConflictHandlerType { get; protected set; }
    }
}
