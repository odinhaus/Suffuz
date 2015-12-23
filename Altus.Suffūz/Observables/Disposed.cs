using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public class Disposed<T> : Operation<T> where T : class, new()
    {
        public Disposed(string globalKey,
            OperationState state,
            T instance)
            : base(globalKey, state, OperationMode.Disposed, "dtor", instance, EventClass.Ephemeral, EventOrder.NotApplicable)
        { }
    }
}
