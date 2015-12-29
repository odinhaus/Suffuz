using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public enum OrderedEventType
    {
        /// <summary>
        /// Occurs in time order, based on timestamps
        /// </summary>
        Temporal = 3,
        /// <summary>
        /// Occurs in logical order, based on synchronization epoch order
        /// </summary>
        Logical = 4,
        /// <summary>
        /// Use a custom handler type to reconcile conflicts
        /// </summary>
        Custom = 100
    }

    /// <summary>
    /// Marks an observable instance property as having changes that are explicitly set by discrete values in either logical or temporal order.
    /// </summary>
    public class ExplicitEventAttribute : ConflictingEventAttribute
    {
        public ExplicitEventAttribute(OrderedEventType type)
        {
            OrderedEventType = type;

        }

        public ExplicitEventAttribute(Type customConflictHandlerType)
        {
            OrderedEventType = OrderedEventType.Custom;
        }

       
        public OrderedEventType OrderedEventType { get; private set; }
    }
}
