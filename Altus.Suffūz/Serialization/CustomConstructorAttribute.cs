using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Serialization
{
    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
    public class CustomConstructorAttribute : Attribute
    {
        /// <summary>
        /// Defines the ICustomConstructor type to use when creating the type during deserialization
        /// </summary>
        /// <param name="customConstructor">the type which implements ICustomConstructor</param>
        public CustomConstructorAttribute(Type customConstructor)
        {
            if (!customConstructor.Implements(typeof(ICustomConstructor)))
            {
                throw new InvalidOperationException("The type supplied must implement ICustomConstructor<>");
            }
            this.CustomConstructor = customConstructor;
        }

        public Type CustomConstructor { get; private set; }
    }
}
