using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Protocols
{
    public interface IChannelService
    {
        IChannel Create(string channelName);
        ServiceLevels AvailableServiceLevels { get; }
        bool CanCreate(string channelName);
    }
}
