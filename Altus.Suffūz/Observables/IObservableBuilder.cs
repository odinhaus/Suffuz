using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public interface IObservableBuilder
    {
        T Create<T>(T instance, string globalKey) where T : class, new();
    }
}
