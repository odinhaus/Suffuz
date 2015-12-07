using Altus.Suffūz.Protocols;
using Altus.Suffūz.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz
{
    public class Get<TRequest, TResponse>
    {
        internal Get(string channelName, TRequest request)
        {
            ChannelName = channelName;
            Request = request;
        }
        public string ChannelName { get; set; }
        public TRequest Request { get; set; }
        public bool HasArgs { get { return typeof(TRequest) != typeof(NoArgs); } }
        public bool HasReturn { get { return typeof(TRequest) != typeof(NoReturn); } }
        public TimeSpan Timeout { get; set; }
    }

    public static class Get<TResponse>
    {
        public static Get<NoArgs, TResponse> From(string channelId)
        {
            return From<NoArgs>(channelId, NoArgs.Empty);
        }

        public static Get<TRequest, TResponse> From<TRequest>(string channelId, TRequest request)
        {
            return new Get<TRequest, TResponse>(channelId, request) { Timeout = DefaultTimeout };
        }

        public static TimeSpan DefaultTimeout
        {
            get
            {
                return Get.DefaultTimeout;
            }
            set
            {
                Get.DefaultTimeout = value;
            }
        }
    }

    public static class Get
    {
        static Get()
        {
            DefaultTimeout = TimeSpan.FromSeconds(5);
        }

        public static Get<TRequest, NoReturn> From<TRequest>(string channelId, TRequest request)
        {
            return new Get<TRequest, NoReturn>(channelId, request) { Timeout = TimeSpan.FromMilliseconds(0) };
        }

        public static Get<NoArgs, NoReturn> From(string channelId)
        {
            return new Get<NoArgs, NoReturn>(channelId, NoArgs.Empty) { Timeout = TimeSpan.FromMilliseconds(0) };
        }

        public static TimeSpan DefaultTimeout { get; set; }
    }
}
