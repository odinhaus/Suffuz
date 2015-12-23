using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public class Created<T> : Operation<T> where T : class, new()
    {   
        public Created(string globalKey,
            OperationState state,
            T instance)
            : base(globalKey, state, OperationMode.Created, "ctor", instance, EventClass.Ephemeral, EventOrder.NotApplicable)
        { }
    }
}
