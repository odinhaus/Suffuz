using System;
using System.Collections.Generic;
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
    }
}
