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
        public ulong InstanceId
        {
            get
            {
                return ulong.Parse(ConfigurationManager.AppSettings["instanceId"]);
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
            return new TypeResolver(
                new Container(c =>
            {
                c.For<ISerializationContext>().Use<SerializationContext>();
                c.For<IServiceRouter>().Use<ServiceRouter>().Singleton();
                c.For<IChannelService>().Use<MulticastChannelService>().Singleton();
                c.For<IBinarySerializerBuilder>().Use<BinarySerializerBuilder>().Singleton();
                c.For<ISerializer>().Use<ComplexSerializer>();
            }));
        }
    }
}
