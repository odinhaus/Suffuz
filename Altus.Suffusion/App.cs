
using StructureMap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion
{
    public class App<T> : App
        where T : Altus.Suffusion.IBootstrapper, new()
    {
        protected App() { }
        public static void Initialize()
        {
            var init = new T();
            Container = init.Initialize();
            var id = Environment.TickCount;
            //InstanceName = init.InstanceName;
            //InstanceId = init.InstanceId;
            InstanceId = (ulong)id;
            InstanceName = id.ToString();
            InstanceCryptoKey = init.InstanceCryptoKey;
        }
    }

    public class App
    {
        protected App() { }
        public static IContainer Container { get; protected set; }
        public static string InstanceName { get; protected set; }
        public static ulong InstanceId { get; protected set; }
        public static byte[] InstanceCryptoKey { get; protected set; }
        public static X Resolve<X>()
        {
            return Container.GetInstance<X>();
        }
    }
}
