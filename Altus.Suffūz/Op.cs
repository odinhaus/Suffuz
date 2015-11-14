using Altus.Suffūz.Protocols;
using Altus.Suffūz.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz
{
    public class Op<TRequest, TResponse>
    {
        internal Op(string channelName, TRequest request)
        {
            ChannelName = channelName;
            Request = request;
        }
        public string ChannelName { get; set; }
        public TRequest Request { get; set; }
        public bool HasArgs { get { return typeof(TRequest) != typeof(NoArgs); } }
        public bool HasReturn { get { return typeof(TRequest) != typeof(NoReturn); } }
    }

    public static class Op<TResponse>
    {
        public static Op<TRequest, TResponse> New<TRequest>(string channelId, TRequest request)
        {
            return new Op<TRequest, TResponse>(channelId, request);
        }
    }

    public static class Op
    {
        public static Op<TRequest, NoReturn> New<TRequest>(string channelId, TRequest request)
        {
            return new Op<TRequest, NoReturn>(channelId, request);
        }

        public static Op<NoArgs, NoReturn> New(string channelId)
        {
            return new Op<NoArgs, NoReturn>(channelId, NoArgs.Empty);
        }
    }
}
