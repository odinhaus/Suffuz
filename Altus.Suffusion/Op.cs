using Altus.Suffusion.Protocols;
using Altus.Suffusion.Routing;
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


    //public class Op<TResponse> : Op<NoArgs, TResponse>
    //{
    //    public Op(string channelName) : base(channelName, new NoArgs()) { }
    //}

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
