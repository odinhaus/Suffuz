using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.DependencyInjection
{
    /// <summary>
    /// Adapter interface for utilizing 3rd party DI strategies to resolve/create type instances
    /// </summary>
    public interface IResolveTypes
    {
        /// <summary>
        /// Returns an instance of type T, as configured by the DI system
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>an instance of T</returns>
        T Resolve<T>();
        /// <summary>
        /// Returns a list of instances of T, as configured by the DI system
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>and enumerated list of instances of T</returns>
        IEnumerable<T> ResolveAll<T>();
    }
}
