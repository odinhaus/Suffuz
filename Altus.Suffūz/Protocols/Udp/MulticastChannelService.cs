﻿using Altus.Suffūz.Routing;
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
    public class MulticastChannelService : IPChannelService
    {
        protected bool _loopback = true;
        protected int _ttl = 2;
        public MulticastChannelService()
        {
            try
            {
                _loopback = bool.Parse(ConfigurationManager.AppSettings["multicastLoopback"]);
            }
            catch { }
            try
            {
                _ttl = int.Parse(ConfigurationManager.AppSettings["multicastTTL"]);
            }
            catch { }
        }

       
        public override ServiceLevels AvailableServiceLevels
        {
            get
            {
                return ServiceLevels.Default;
            }
        }

        protected override IChannel Create(string uri, IPEndPoint endpoint)
        {
            var channel = new MulticastChannel(App.Resolve<IChannelBuffer<UdpMessage>>(), uri, endpoint, true, !_loopback, _ttl);
            lock (_channels)
            {
                _channels.Add(uri, channel);
            }

            return channel;
        }
    }
}
