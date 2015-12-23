using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public class MethodCall<T, U> : Operation<T> where T : class, new()
    {
        public MethodCall(string globalKey,
            OperationState state,
            string memberName,
            Type instanceType,
            T instance,
            RuntimeArgument[] args)
            : base(globalKey, state, OperationMode.MethodCall, memberName, instance, EventClass.Ephemeral, EventOrder.NotApplicable)
        {
            this.Arguments = args;
        }

        public MethodCall(string globalKey,
            OperationState state,
            string memberName,
            Type instanceType,
            T instance,
            RuntimeArgument[] args, 
            U returnValue)
            : base(globalKey, state, OperationMode.MethodCall, memberName, instance, EventClass.Ephemeral, EventOrder.NotApplicable)
        {
            this.Arguments = args;
            this.ReturnValue = returnValue;
        }
        public RuntimeArgument[] Arguments { get; private set; }
        public U ReturnValue { get; private set; }
    }
}
