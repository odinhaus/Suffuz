using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables.Tests.Observables
{
    public class StateClass : BaseState
    {
        [CommutativeEvent(CommutativeEventType.Additive)]
        public virtual int Size { get; set; }

        [CommutativeEvent(CommutativeEventType.Multiplicative)]
        public virtual double Score { get; set; }

        [ExplicitEvent(OrderedEventType.Logical)]
        public virtual string Name { get; set; }

        [ExplicitEvent(OrderedEventType.Temporal)]
        public virtual DateTime LastUpdated { get; set; }

        //[SequentialEvent(OrderedEventType.Logical)]
        public virtual List<string> Names { get; set; }

        //[SequentialEvent(OrderedEventType.Temporal)]
        public virtual List<DateTime> Updates { get; set; }


        public virtual int Hello(string message)
        {
            return 1;
        }
    }

    public abstract class BaseState
    {
        [BinarySerializable(0)]
        [CommutativeEvent(CommutativeEventType.Additive)]
        public virtual int Age { get; set; }
    }
}
