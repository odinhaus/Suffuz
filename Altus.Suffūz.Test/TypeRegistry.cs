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
