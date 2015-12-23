using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    /// <summary>
    /// Marks an observable instance property as having changes that are applied in an ordered sequencem either in a logical or temporal sense.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class SequentialEventAttribute : Attribute
    {
        public SequentialEventAttribute(OrderedEventType type)
        {
            OrderedEventType = type;
        }
        public OrderedEventType OrderedEventType { get; private set; }
    }
}
