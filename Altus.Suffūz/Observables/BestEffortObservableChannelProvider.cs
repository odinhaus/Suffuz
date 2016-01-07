using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altus.Suffūz.Protocols;
using System.Net;
using System.Configuration;

namespace Altus.Suffūz.Observables
{
    public class BestEffortObservableChannelProvider : IObservableChannelProvider
    {
        static readonly string _defaultChannelName = "observable_default";

        static BestEffortObservableChannelProvider()
        {
            IPEndPoint defaultEndPoint, configuredEndPoint = null;
            IPEndPointEx.TryParseEndPoint("230.0.0.1:5000", out defaultEndPoint);
            try
            {
                IPEndPointEx.TryParseEndPoint(ConfigurationManager.AppSettings["defaultObservableEndPoint"], out configuredEndPoint);
            }
            catch { }
            DefaultEndPoint = configuredEndPoint ?? defaultEndPoint;
            DefaultChannelService = App.ResolveAll<IChannelService>()
                .FirstOrDefault(cs => cs.AvailableServiceLevels == ServiceLevels.BestEffort);
            if (!DefaultChannelService.CanCreate(_defaultChannelName))
            {
                DefaultChannelService.Register(_defaultChannelName, DefaultEndPoint, TimeSpan.FromSeconds(5));
            }
        }

        public BestEffortObservableChannelProvider()
            : this((op) => GetDefaultChannel(op))
        { }


        public BestEffortObservableChannelProvider(Func<Operation, IEnumerable<IChannel>> channelSelector)
        {
            ChannelSelector = channelSelector;
        }

        public IEnumerable<IChannel> GetChannels(Operation operation)
        {
            return ChannelSelector(operation);
        }

        protected static IEnumerable<IChannel> GetDefaultChannel(Operation op)
        {
            yield return DefaultChannelService.Create(_defaultChannelName);
        }

        public Func<Operation, IEnumerable<IChannel>> ChannelSelector { get; private set; }
        public static IPEndPoint DefaultEndPoint { get; private set; }
        public static IChannelService DefaultChannelService { get; private set; }
        public static string DefaultChannelName { get { return _defaultChannelName; } }
    }
}
