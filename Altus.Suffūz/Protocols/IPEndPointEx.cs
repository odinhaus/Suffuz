using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Protocols
{
    public static class IPEndPointEx
    {
        public static bool TryParseEndPoint(this string addressAndPort, out IPEndPoint endPoint)
        {
            endPoint = null;
            try
            {
                string[] parts = addressAndPort.Split(':');
                if (parts.Length != 2) return false;
                endPoint = new IPEndPoint(IPAddress.Parse(parts[0].Trim()), int.Parse(parts[1].Trim()));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static IPAddress _ipAddress = null;

        public static IPAddress LocalAddress(bool useCached)
        {
            if (!useCached || _ipAddress == null || IPAddress.IsLoopback(_ipAddress))
            {
                IPAddress ip = System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName()).Where(ipa => !IPAddress.IsLoopback(ipa)
                            && ipa.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).FirstOrDefault();

                if (ip == null)
                    ip = IPAddress.Loopback;

                _ipAddress = ip;
            }
            return _ipAddress;
        }

        public static IPEndPoint LocalEndPoint(int port, bool useCached)
        {
            return new IPEndPoint(LocalAddress(useCached), port);
        }

        public static IPEndPoint Increment(this IPEndPoint value, int incrementAmount = 1, string maxIP = "255.255.255.255")
        {
            var currentIPValue = IpBytesToUint(value.Address.GetAddressBytes());
            var maxIPValue = IpBytesToUint(IPAddress.Parse(maxIP).GetAddressBytes());

            if (currentIPValue++ <= maxIPValue)
            {
                return new IPEndPoint(new IPAddress(ReverseBytesArray(currentIPValue)), value.Port);
            }
            else
            {
                throw new InvalidOperationException("Max address reached.");
            }
        }

        public static IPEndPoint Decrement(this IPEndPoint value, int decrementAmount = 1, string minIP = "0.0.0.0")
        {
            var currentIPValue = IpBytesToUint(value.Address.GetAddressBytes());
            var minIPValue = IpBytesToUint(IPAddress.Parse(minIP).GetAddressBytes());

            if (currentIPValue-- >= minIPValue)
            {
                return new IPEndPoint(new IPAddress(ReverseBytesArray(currentIPValue)), value.Port);
            }
            else
            {
                throw new InvalidOperationException("Min address reached.");
            }
        }

        public static IEnumerable<string> GetIPRange(this IPAddress startIP,
            IPAddress endIP)
        {
            uint sIP = IpBytesToUint(startIP.GetAddressBytes());
            uint eIP = IpBytesToUint(endIP.GetAddressBytes());
            while (sIP <= eIP)
            {
                yield return new IPAddress(ReverseBytesArray(sIP)).ToString();
                sIP++;
            }
        }


        /* reverse byte order in array */
        private static uint ReverseBytesArray(uint ip)
        {
            byte[] bytes = BitConverter.GetBytes(ip);
            bytes = bytes.Reverse().ToArray();
            return (uint)BitConverter.ToInt32(bytes, 0);
        }


        /* Convert bytes array to 32 bit long value */
        public static uint IpBytesToUint(this byte[] ipBytes)
        {
            var bConvert = new ByteConverter();
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
