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
    public class MulticastChannelService : IChannelService
    {
        bool _loopback = false;
        public MulticastChannelService()
        {
            _loopback = bool.Parse(ConfigurationManager.AppSettings["multicastLoopback"]);
        }

        Dictionary<string, IPEndPoint> _endPoints = new Dictionary<string, IPEndPoint>();
        public MulticastChannelService Register(string channel, IPEndPoint endpoint)
        {
            _endPoints[channel] = endpoint;
            return this;
        }

        Dictionary<string, IChannel> _channels = new Dictionary<string, IChannel>();
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

        private IChannel Create(string uri, IPEndPoint endpoint)
        {
            var channel = new MulticastChannel(endpoint, true, !_loopback);
            lock (_channels)
            {
                _channels.Add(uri, channel);
            }

            return channel;
        }
    }
}
