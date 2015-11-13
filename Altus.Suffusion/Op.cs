using Altus.Suffusion.Protocols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion
{
    public class Op<TRequest, TResponse>
    {
        public Op(string channelName, TRequest request)
        {
            ChannelName = channelName;
            Request = request;
        }
        public string ChannelName { get; set; }
        public TRequest Request { get; set; }
    }


    public class Op<TResponse> : Op<NoArgs, TResponse>
    {
        public Op(string channelName) : base(channelName, new NoArgs()) { }
    }
}
