using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion.Routing
{
    public class Aggregate<TResponse>
    {
        public IEnumerable<TResponse> Responses { get; private set; }
        public void Add(TResponse response)
        {

        }

        public bool IsComplete { get; set; }
    }
}
