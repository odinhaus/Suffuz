using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public class PropertyUpdate<T, U> : Operation<T> where T : class, new()
    {
        public PropertyUpdate(string globalKey,
            OperationState state,
            string memberName,
            Type instanceType,
            T instance,
            EventClass @class,
            EventOrder order,
            U baseValue,
            U newValue)
            :base(globalKey, state, OperationMode.PropertyChanged, memberName, instance, @class, order)
        {
            this.BaseValue = baseValue;
            this.NewValue = newValue;
        }
        public U BaseValue { get; set; }
        public U NewValue { get; set; }
        public override object Value
        {
            get
            {
                return NewValue;
            }
        }
    }
}
