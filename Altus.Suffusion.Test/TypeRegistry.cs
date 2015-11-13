using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altus.Suffusion.DependencyInjection;
using System.Configuration;
using StructureMap;
using Altus.Suffusion.Serialization;
using Altus.Suffusion.Routing;
using Altus.Suffusion.Protocols;
using Altus.Suffusion.Serialization.Binary;
using Altus.Suffusion.Protocols.Udp;

namespace Altus.Suffusion.Test
{
    public class TypeRegistry : IBootstrapper
    {
        public byte[] InstanceCryptoKey
        {
            get
            {
                return Convert.FromBase64String(ConfigurationManager.AppSettings["instanceCryptoKey"]);
            }
        }

        public ulong InstanceId
        {
            get
            {
                return ulong.Parse(ConfigurationManager.AppSettings["instanceId"]);
            }
        }

        public string InstanceName
        {
            get
            {
                return ConfigurationManager.AppSettings["instanceName"];
            }
        }

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
