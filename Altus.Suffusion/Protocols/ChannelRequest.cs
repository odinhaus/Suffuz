using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altus.Suffusion.Routing;

namespace Altus.Suffusion.Protocols
{
    public class ChannelRequest : ChannelRequest<NoArgs>
    {
        public ChannelRequest(string uri) : this(uri, TimeSpan.FromSeconds(30))
        { }

        public ChannelRequest(string uri, TimeSpan timeout) : this(uri, timeout, new string[] { "*" })
        { }

        public ChannelRequest(string uri, TimeSpan timeout, string[] recipients)
            : base(uri, timeout, NoArgs.Empty, recipients)
        {
            
        }
    }

    public class ChannelRequest<TRequest>
    {
        public ChannelRequest(string uri) : this(uri, TimeSpan.FromSeconds(30))
        { }

        public ChannelRequest(string uri, TimeSpan timeout)
        {
            Uri = uri;
            Timeout = timeout;
            Recipients = new string[] { "*" };
        }

        public ChannelRequest(string uri, TimeSpan timeout, TRequest payload)
            : this(uri, timeout, payload, new string[] { "*" })
        { }

        public ChannelRequest(string uri, TimeSpan timeout, TRequest payload, string[] recipients)
        {
            Uri = uri;
            Timeout = timeout;
            Payload = payload;
            Recipients = recipients;
            ServiceType = ServiceType.RequestResponse;
        }
        public string Uri { get; set; }
        public string[] Recipients { get; set; }
        public TRequest Payload { get; set; }
        public TimeSpan Timeout { get; set; }
        public ServiceType ServiceType { get; set; }

        public static implicit operator ChannelRequest<TRequest>(string uri)
        {
            return new ChannelRequest<TRequest>(uri);
        }

        public static implicit operator string(ChannelRequest<TRequest> request)
        {
            return request.Uri;
        }
    }
}
