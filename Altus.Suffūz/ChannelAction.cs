using Altus.Suffūz.Protocols;
using Altus.Suffūz.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz
{
    public class ChannelAction<TRequest, TResponse>
    {
        internal ChannelAction(string channelName, TRequest request)
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

    /// <summary>
    /// Posts messages to a channel and provides responses
    /// </summary>
    /// <typeparam name="TRequest">The request type that will be sent</typeparam>
    /// <typeparam name="TResponse">The response type that will be returned when the request is executed</typeparam>
    public static class Post<TRequest, TResponse>
    {
        public static ChannelAction<TRequest, TResponse> Via(string channelId, TRequest request)
        {
            return new ChannelAction<TRequest, TResponse>(channelId, request) { Timeout = DefaultTimeout };
        }

        public static TimeSpan DefaultTimeout
        {
            get
            {
                return Put.DefaultTimeout;
            }
            set
            {
                Put.DefaultTimeout = value;
            }
        }
    }

    /// <summary>
    /// Posts messages to a channel and provides responses
    /// </summary>
    /// <typeparam name="TResponse">The response type that will be returned when the request is executed</typeparam>
    public static class Post<TResponse>
    {
        public static ChannelAction<NoArgs, TResponse> Via(string channelId)
        {
            return new ChannelAction<NoArgs, TResponse>(channelId, NoArgs.Empty) { Timeout = DefaultTimeout };
        }

        public static TimeSpan DefaultTimeout
        {
            get
            {
                return Put.DefaultTimeout;
            }
            set
            {
                Put.DefaultTimeout = value;
            }
        }
    }

    public static class Post
    {
        public static ChannelAction<NoArgs> Via(string channelId)
        {
            return new ChannelAction<NoArgs>(channelId, NoArgs.Empty) { Timeout = DefaultTimeout };
        }
        public static ChannelAction<TRequest> Via<TRequest>(string channelId, TRequest request)
        {
            return new ChannelAction<TRequest>(channelId, request) { Timeout = DefaultTimeout };
        }


        public static TimeSpan DefaultTimeout
        {
            get
            {
                return Put.DefaultTimeout;
            }
            set
            {
                Put.DefaultTimeout = value;
            }
        }
    }

    public class ChannelAction<TRequest>
    {
        internal ChannelAction(string channelName, TRequest request)
        {
            ChannelName = channelName;
            Request = request;
        }

        public string ChannelName { get; private set; }
        public TRequest Request { get; private set; }
        public TimeSpan Timeout { get; set; }

        public ChannelAction<TRequest, TResponse> Return<TResponse>()
        {
            return new ChannelAction<TRequest, TResponse>(ChannelName, Request) { Timeout = Timeout };
        }
    }

    /// <summary>
    /// Puts messages onto the specified channel with no responses
    /// </summary>
    public static class Put
    {
        static Put()
        {
            DefaultTimeout = TimeSpan.FromSeconds(5);
        }

        public static ChannelAction<TRequest, NoReturn> Via<TRequest>(string channelId, TRequest request)
        {
            return new ChannelAction<TRequest, NoReturn>(channelId, request) { Timeout = TimeSpan.FromMilliseconds(0) };
        }

        public static ChannelAction<NoArgs, NoReturn> Via(string channelId)
        {
            return new ChannelAction<NoArgs, NoReturn>(channelId, NoArgs.Empty) { Timeout = TimeSpan.FromMilliseconds(0) };
        }

        public static TimeSpan DefaultTimeout { get; set; }
    }
}
