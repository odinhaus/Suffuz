using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Protocols
{
    public interface IChannelService
    {
        IChannelService Register(string channel, IPEndPoint endpoint);
        IChannelService Register(string channel, IPEndPoint endpoint, TimeSpan ttl);
        IChannel Create(string channelName);
        ServiceLevels AvailableServiceLevels { get; }
        bool CanCreate(string channelName);
    }
}
