using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Test
{
    /// <summary>
    /// Simple Channel mapping constants
    /// </summary>
    public class Channels
    {
        public static readonly string CHANNEL = "channel1";
        public static readonly IPEndPoint CHANNEL_EP = new IPEndPoint(IPAddress.Parse("224.0.0.0"), 5000);

        public static readonly string BESTEFFORT_CHANNEL = "channel2";
        public static readonly IPEndPoint BESTEFFORT_CHANNEL_EP = new IPEndPoint(IPAddress.Parse("224.0.0.1"), 5000);
    }
}
