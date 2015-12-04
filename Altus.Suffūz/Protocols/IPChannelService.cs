using Altus.Suffūz.Routing;
using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Protocols.Udp
{
    public abstract class IPChannelService : IChannelService
    {
        Dictionary<string, IPEndPoint> _endPoints = new Dictionary<string, IPEndPoint>();
        public IChannelService Register(string channel, IPEndPoint endpoint)
        {
            _endPoints[channel] = endpoint;
            return this;
        }

        protected Dictionary<string, IChannel> _channels = new Dictionary<string, IChannel>();

        public abstract ServiceLevels AvailableServiceLevels { get; }
        protected abstract IChannel Create(string uri, IPEndPoint endpoint);

        public IChannel Create(string uri)
        {
            IChannel channel;
            bool exists;

            lock(_channels)
            {
                exists = _channels.TryGetValue(uri, out channel);
            }

            if (!exists)
            {
                IPEndPoint endpoint;
                if (_endPoints.TryGetValue(uri, out endpoint))
                    channel = Create(uri, endpoint);
                else
                    throw new InvalidOperationException("The channel provided has not been mapped to an end point.");
            }
            
            return channel;
        }

        public bool CanCreate(string channelName)
        {
            return _channels.ContainsKey(channelName);
        }
    }
}
