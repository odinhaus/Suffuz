using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion.Protocols
{
    public interface IChannelService
    {
        IChannel Create(string channelName);
    }
}
