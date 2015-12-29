using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public enum CommutativeEventType : byte
    {
        /// <summary>
        /// Indicates that the result of the event would be determined by multiplying the current value by some known factor
        /// </summary>
        Multiplicative = 1,
        /// <summary>
        /// Indicates that the result of the event would be determined by adding the current value to some known amount
        /// </summary>
        Additive = 2
    }

    /// <summary>
    /// Marks an observable instance property as having changes that can be determined by a commutable mathmatical operation. 
    /// I.e. by either either adding a series of changes together to the current value to produce a new value, or multiplying 
    /// the current value by a sequence of factors to determine a new value.
    /// </summary>
    public class CommutativeEventAttribute : ConflictingEventAttribute
    {
        public CommutativeEventAttribute(CommutativeEventType type)
        {
            CommutativeEventType = type;
        }
        /// <summary>
        /// Gets the event type determining how the current value will be changed
        /// </summary>
        public CommutativeEventType CommutativeEventType { get; private set; }
    }
}
