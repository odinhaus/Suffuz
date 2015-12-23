using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public class AnyOperation<T> : Operation<T> where T : class, new()
    {
        public AnyOperation(string globalKey,
            OperationState state,
            OperationMode mode,
            string memberName,
            T instance,
            EventClass @class,
            EventOrder order,
            Operation<T> discreteOperation)
            : base(globalKey, state, mode, memberName, instance, @class, order)
        {
            this.DiscreteOperation = discreteOperation;
        }

        public Operation<T> DiscreteOperation { get; private set; }
    }
}
