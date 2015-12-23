using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public abstract class Subscription : IDisposable
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    public class Subscription<T> : Subscription where T : class, new()
    {
       
    }
}
