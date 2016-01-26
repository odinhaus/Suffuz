using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public interface IObservableBuilder
    {
        object Create(object instance, string globalKey, IPublisher publisher);
        T Create<T>(T instance, string globalKey, IPublisher publisher) where T : class, new();
    }
}
