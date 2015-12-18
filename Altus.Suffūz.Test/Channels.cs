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
        /// <summary>
        /// channel1
        /// </summary>
        public static readonly string CHANNEL = "channel1";
        /// <summary>
        /// 224.0.0.0
        /// </summary>
        public static readonly IPEndPoint CHANNEL_EP = new IPEndPoint(IPAddress.Parse("224.0.0.0"), 5000);
        /// <summary>
        /// channel2
        /// </summary>
        public static readonly string BESTEFFORT_CHANNEL = "channel2";
        /// <summary>
        /// 224.0.0.1
        /// </summary>
        public static readonly IPEndPoint BESTEFFORT_CHANNEL_EP = new IPEndPoint(IPAddress.Parse("224.0.0.1"), 5000);
        /// <summary>
        /// 30 seconds
        /// </summary>
        public static readonly TimeSpan BESTEFFORT_CHANNEL_TTL = TimeSpan.FromSeconds(30);
    }
}
