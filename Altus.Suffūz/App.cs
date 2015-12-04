
using Altus.Suffūz.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public static X Resolve<X>()
        {
            try { return Container.Resolve<X>(); }
            catch { return default(X); }
        }

        public static IEnumerable<X> ResolveAll<X>()
        {
            try { return Container.ResolveAll<X>(); }
            catch { return new X[0]; }
        }
    }
}
