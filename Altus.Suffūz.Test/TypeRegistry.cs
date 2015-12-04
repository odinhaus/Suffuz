using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altus.Suffūz.DependencyInjection;
using System.Configuration;
using StructureMap;
using Altus.Suffūz.Serialization;
using Altus.Suffūz.Routing;
using Altus.Suffūz.Protocols;
using Altus.Suffūz.Serialization.Binary;
using Altus.Suffūz.Protocols.Udp;
using Altus.Suffūz.Collections;

namespace Altus.Suffūz.Test
{
    /// <summary>
    /// Sample Bootstrapper reading from configuration, and providing a dependency resolver, with basic DI Mappings for StructureMap
    /// </summary>
    public class TypeRegistry : IBootstrapper
    {
        /// <summary>
        /// Any byte[] that can be used in the creation of message hashes when communicating with other nodes
        /// </summary>
        public byte[] InstanceCryptoKey
        {
            get
            {
                return Convert.FromBase64String(ConfigurationManager.AppSettings["instanceCryptoKey"]);
            }
        }
        /// <summary>
        /// Globally unique Id for this node
        /// </summary>
        public ushort InstanceId
        {
            get
            {
                return ushort.Parse(ConfigurationManager.AppSettings["instanceId"]);
            }
        }
        /// <summary>
        /// Globally unique Name for this node
        /// </summary>
        public string InstanceName
        {
            get
            {
                return ConfigurationManager.AppSettings["instanceName"];
            }
        }
        /// <summary>
        /// Returns the DI type resolver adapter
        /// </summary>
        /// <returns></returns>
        public IResolveTypes Initialize()
        {
            var channelService = new MulticastChannelService();
            // create our channel mappings
            channelService.Register(Channels.CHANNEL, Channels.CHANNEL_EP);

            var beChannelService = new BestEffortMulticastChannelService();
            // create our channel mappings
            beChannelService.Register(Channels.BESTEFFORT_CHANNEL, Channels.BESTEFFORT_CHANNEL_EP);

            return new TypeResolver(
                new Container(c =>
            {
                c.For<ISerializationContext>().Use<SerializationContext>();
                c.For<IServiceRouter>().Use<ServiceRouter>().Singleton();
                // use the mapped channels above
                c.For<IChannelService>().Use<MulticastChannelService>(channelService).Singleton();
                c.For<IChannelService>().Use<BestEffortMulticastChannelService>(beChannelService).Singleton();
                c.For<IBinarySerializerBuilder>().Use<ILSerializerBuilder>().Singleton();
                c.For<ISerializer>().Use<ComplexSerializer>();
                c.For<IManagePersistentCollections>().Use<PersistentCollectionManager>();
            }));
        }
    }
}
