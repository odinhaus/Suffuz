using Altus.Suffūz.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StructureMap;

namespace Altus.Suffūz.Test
{
    /// <summary>
    /// StructureMap DI adapter for Suffusion 
    /// </summary>
    public class TypeResolver : IResolveTypes
    {
        private IContainer _container;

        public TypeResolver(IContainer container)
        {
            _container = container;
        }
        public T Resolve<T>()
        {
            return _container.GetInstance<T>();
        }

        public IEnumerable<T> ResolveAll<T>()
        {
            return _container.GetAllInstances<T>();
        }
    }
}
