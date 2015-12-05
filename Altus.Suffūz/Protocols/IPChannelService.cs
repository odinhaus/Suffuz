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
        Dictionary<string, TimeSpan> _ttls = new Dictionary<string, TimeSpan>();
        public IChannelService Register(string channel, IPEndPoint endpoint)
        {
            return Register(channel, endpoint, TimeSpan.FromSeconds(90));
        }

        public IChannelService Register(string channel, IPEndPoint endpoint, TimeSpan ttl)
        {
            _endPoints[channel] = endpoint;
            _ttls[channel] = ttl;
            return this;
        }

        protected Dictionary<string, IChannel> _channels = new Dictionary<string, IChannel>();

        public abstract ServiceLevels AvailableServiceLevels { get; }
        protected abstract IChannel Create(string channel, IPEndPoint endpoint);

        public IChannel Create(string channelName)
        {
            IChannel channel;
            bool exists;

            lock(_channels)
            {
                exists = _channels.TryGetValue(channelName, out channel);
            }

            if (!exists)
            {
                IPEndPoint endpoint;
                if (_endPoints.TryGetValue(channelName, out endpoint))
                {
                    channel = Create(channelName, endpoint);
                    channel.TTL = _ttls[channelName];
                }
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
