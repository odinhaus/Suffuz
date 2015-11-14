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
        IPAddress _minIP, _maxIP, _nextIP;
        bool _loopback = false;
        public MulticastChannelService()
        {
            var range = ConfigurationManager.AppSettings["multicastRange"].Split('-');
            _minIP = IPAddress.Parse(range[0]);
            _maxIP = IPAddress.Parse(range[1]);
            _nextIP = _minIP;
            _loopback = bool.Parse(ConfigurationManager.AppSettings["multicastLoopback"]);
        }

        Dictionary<string, IChannel> _channels = new Dictionary<string, IChannel>();
        public IChannel Create(string uri)
        {
            IChannel channel;
            lock(_channels)
            {
                if (!_channels.TryGetValue(uri, out channel))
                {
                    Increment();
                    channel = new MulticastChannel(new IPEndPoint(_nextIP, 5000), true, !_loopback);
                    _channels.Add(uri, channel);
                }
            }
            return channel;
        }

        private void Increment()
        {
            var currentIP = IpToUint(_nextIP.GetAddressBytes());
            var maxIP = IpToUint(_maxIP.GetAddressBytes());

            if (currentIP++ <= maxIP)
            {
                _nextIP = new IPAddress(ReverseBytesArray(currentIP));
            }
            else
            {
                throw new InvalidOperationException("No more channels available");
            }
        }

        public IEnumerable<string> GetIPRange(IPAddress startIP,
            IPAddress endIP)
        {
            uint sIP = IpToUint(startIP.GetAddressBytes());
            uint eIP = IpToUint(endIP.GetAddressBytes());
            while (sIP <= eIP)
            {
                yield return new IPAddress(ReverseBytesArray(sIP)).ToString();
                sIP++;
            }
        }


        /* reverse byte order in array */
        protected uint ReverseBytesArray(uint ip)
        {
            byte[] bytes = BitConverter.GetBytes(ip);
            bytes = bytes.Reverse().ToArray();
            return (uint)BitConverter.ToInt32(bytes, 0);
        }


        /* Convert bytes array to 32 bit long value */
        protected uint IpToUint(byte[] ipBytes)
        {
            ByteConverter bConvert = new ByteConverter();
            uint ipUint = 0;

            int shift = 24; // indicates number of bits left for shifting
            foreach (byte b in ipBytes)
            {
                if (ipUint == 0)
                {
                    ipUint = (uint)bConvert.ConvertTo(b, typeof(uint)) << shift;
                    shift -= 8;
                    continue;
                }

                if (shift >= 8)
                    ipUint += (uint)bConvert.ConvertTo(b, typeof(uint)) << shift;
                else
                    ipUint += (uint)bConvert.ConvertTo(b, typeof(uint));

                shift -= 8;
            }

            return ipUint;
        }
    }
}
