using Altus.Suffūz.Collections;
using Altus.Suffūz.DependencyInjection;
using Altus.Suffūz.Observables;
using Altus.Suffūz.Observables.Serialization.Binary;
using Altus.Suffūz.Protocols;
using Altus.Suffūz.Protocols.Udp;
using Altus.Suffūz.Routing;
using Altus.Suffūz.Scheduling;
using Altus.Suffūz.Serialization;
using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz
{
    public class App<T> : App
        where T : Altus.Suffūz.IBootstrapper, new()
    {
        protected App() { }
        public static void Initialize()
        {
            var init = new T();
            Container = init.Initialize();
            var id = Environment.TickCount;
            InstanceName = init.InstanceName;
            InstanceId = init.InstanceId;
            InstanceCryptoKey = init.InstanceCryptoKey;
        }
    }

    public class App
    {
        protected App() { }

        protected static IResolveTypes Container { get; set; }
        public static string InstanceName { get; protected set; }
        public static ushort InstanceId { get; protected set; }
        public static byte[] InstanceCryptoKey { get; protected set; }

        static Dictionary<Type, List<Delegate>> _defaults = new Dictionary<Type, List<Delegate>>();
        public static X Resolve<X>()
        {
            lock(_defaults)
            {
                List<Delegate> resolvers;
                if (_defaults.TryGetValue(typeof(X), out resolvers))
                {
                    return (X)resolvers.First().DynamicInvoke();
                }
                else
                {
                    try { return Container.Resolve<X>(); }
                    catch
                    {
                        var type = typeof(X);
                        resolvers = GetDefaultResolvers<X>();
                        _defaults.Add(type, resolvers);
                        return Resolve<X>();
                    }
                }
            }
        }

        public static IEnumerable<X> ResolveAll<X>()
        {
            lock (_defaults)
            {
                List<Delegate> resolvers;
                if (_defaults.TryGetValue(typeof(X), out resolvers))
                {
                    foreach(var del in resolvers)
                    {
                        var x = (X)del.DynamicInvoke();
                        if (x != null)
                            yield return x;
                    }
                }
                else
                {
                    IEnumerable<X> resolved;
                    try
                    {
                        resolved = Container.ResolveAll<X>();
                        if (resolved.Count() == 0)
                        {
                            var type = typeof(X);
                            resolvers = GetDefaultResolvers<X>();
                            _defaults.Add(type, resolvers);
                            resolved = ResolveAll<X>();
                        }
                    }
                    catch
                    {
                        var type = typeof(X);
                        resolvers = GetDefaultResolvers<X>();
                        _defaults.Add(type, resolvers);
                        resolved = ResolveAll<X>();
                    }
                    foreach(var x in resolved)
                    {
                        yield return x;
                    }
                }
            }
        }

        private static List<Delegate> GetDefaultResolvers<X>()
        {
            var type = typeof(X);
            List<Delegate> resolvers;
            if (type == typeof(IBestEffortChannelBuffer<UdpMessage>))
            {
                resolvers = new List<Delegate> { new Func<IBestEffortChannelBuffer<UdpMessage>>(() => new BestEffortChannelBuffer()) };
            }
            else if (type == typeof(IChannelBuffer<UdpMessage>))
            {
                resolvers = new List<Delegate> { new Func<IChannelBuffer<UdpMessage>>(() => new ChannelBuffer()) };
            }
            else if (type == typeof(ISerializationContext))
            {
                resolvers = new List<Delegate> { new Func<ISerializationContext>(() => new SerializationContext()) };
            }
            else if (type == typeof(IServiceRouter))
            {
                var router = new ServiceRouter();
                resolvers = new List<Delegate> { new Func<IServiceRouter>(() => router) };
            }
            else if (type == typeof(IChannelService))
            {
                var channelService = new MulticastChannelService();
                var beChannelService = new BestEffortMulticastChannelService();
                resolvers = new List<Delegate>
                            {
                                new Func<IChannelService>(() => channelService),
                                new Func<IChannelService>(() => beChannelService)
                            };
            }
            else if (type == typeof(IBinarySerializerBuilder))
            {
                var builder = new ILSerializerBuilder();
                resolvers = new List<Delegate> { new Func<IBinarySerializerBuilder>(() => builder) };
            }
            else if (type == typeof(ISerializer))
            {
                resolvers = new List<Delegate>
                            {
                                new Func<ISerializer>(() => new ComplexSerializer(App.Resolve<IBinarySerializerBuilder>())),
                                new Func<ISerializer>(() => new MessageSegmentSerializer()),
                                new Func<ISerializer>(() => new UdpMessageSerializer()),
                                new Func<ISerializer>(() => new ChangeStateSerializer())
                            };
            }
            else if (type == typeof(IManagePersistentCollections))
            {
                resolvers = new List<Delegate> { new Func<IManagePersistentCollections>(() => new PersistentCollectionManager()) };
            }
            else if (type == typeof(IScheduler))
            {
                resolvers = new List<Delegate> { new Func<IScheduler>(() => Scheduler.Current) };
            }
            else if (type == typeof(IPublisher))
            {
                resolvers = new List<Delegate> { new Func<IPublisher>(() => new Publisher()) };
            }
            else if (type == typeof(IObservableBuilder))
            {
                resolvers = new List<Delegate> { new Func<IObservableBuilder>(() => new ILObservableBuilder(App.Resolve<IPublisher>())) };
            }
            else
            {
                var ctor = typeof(X).GetConstructor(new Type[0]);
                Func<X> ctorFunc;

                if (ctor == null)
                {
                    ctorFunc = new Func<X>(() => default(X));
                }
                else
                {
                    ctorFunc = Expression.Lambda<Func<X>>(Expression.New(ctor)).Compile();
                }

                resolvers = new List<Delegate>
                {
                    new Func<X>(() => default(X))
                };
            }
            return resolvers;
        }
    }
}
