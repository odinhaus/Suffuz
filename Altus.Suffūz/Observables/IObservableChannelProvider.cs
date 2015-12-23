using Altus.Suffūz.Protocols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public interface IObservableChannelProvider
    {
        IEnumerable<IChannel> GetChannels(Operation operation);
    }
}
