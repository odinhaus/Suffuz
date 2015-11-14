using Altus.Suffusion.Protocols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion
{
    public static class Dispatcher
    {
        public static readonly string[] ANY_RECIPIENT = new string[] { "*" };
        public static IChannelService ChannelService { get; private set; }

        static Dispatcher()
        {
            ChannelService = App.Resolve<IChannelService>();
        }

        private static string[] CheckRecipients(string[] recipients)
        {
            if (recipients == null || recipients.Length == 0)
                return ANY_RECIPIENT;
            else
                return recipients;
        }

        public static TResponse Dispatch<TResponse>(string uri, params string[] recipients)
        {
            var channel = ChannelService.Create(uri);
            return channel.Call<TResponse>(
                new ChannelRequest(uri)
                {
                    Recipients = CheckRecipients(recipients)
                });
        }

        public static TResponse DispatchAsync<TResponse>(string uri, TimeSpan timeout, params string[] recipients)
        {
            var channel = ChannelService.Create(uri);
            return channel.Call<TResponse>(
                new ChannelRequest(uri, timeout)
                {
                    Recipients = CheckRecipients(recipients)
                });
        }

        public static TResponse DispatchAsync<TRequest, TResponse>(string uri, TRequest payload, params string[] recipients)
        {
            var channel = ChannelService.Create(uri);
            return channel.Call<TRequest, TResponse>(
                new ChannelRequest<TRequest>(uri, TimeSpan.FromSeconds(30), payload)
                {
                    Recipients = CheckRecipients(recipients)
                });
        }

        public static void DispatchAsync<TRequest>(string uri, TRequest payload, params string[] recipients)
        {
            var channel = ChannelService.Create(uri);
            channel.Call<TRequest>(
                new ChannelRequest<TRequest>(uri, TimeSpan.FromSeconds(0), payload)
                {
                    Recipients = CheckRecipients(recipients),
                    ServiceType = ServiceType.Broadcast
                });
        }

        public static TResponse DispatchAsync<TRequest, TResponse>(string uri, TRequest payload, TimeSpan timeout, params string[] recipients)
        {
            var channel = ChannelService.Create(uri);
            return channel.Call<TRequest, TResponse>(
                new ChannelRequest<TRequest>(uri, timeout, payload)
                {
                    Recipients = CheckRecipients(recipients)
                });
        }

        public static void DispatchAsync<TRequest, TResponse>(string uri, TRequest payload, Func<TResponse, bool> handler, params string[] recipients)
        {
            var channel = ChannelService.Create(uri);
            channel.Call<TRequest, TResponse>(
                new ChannelRequest<TRequest>(uri, TimeSpan.FromSeconds(30), payload)
                {
                    Recipients = CheckRecipients(recipients),
                    ServiceType = ServiceType.Broadcast
                }, handler);
        }

        public static void DispatchAsync<TRequest, TResponse>(string uri, TRequest payload, TimeSpan timeout, Func<TResponse, bool> handler, params string[] recipients)
        {
            var channel = ChannelService.Create(uri);
            channel.Call<TRequest, TResponse>(
                new ChannelRequest<TRequest>(uri, timeout, payload)
                {
                    Recipients = CheckRecipients(recipients),
                    ServiceType = ServiceType.Broadcast
                }, handler);
        }
    }
}
