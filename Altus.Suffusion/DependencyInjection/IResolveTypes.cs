using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion.DependencyInjection
{
    public interface IResolveTypes
    {
        T Resolve<T>();
        IEnumerable<T> ResolveAll<T>();
    }
}
