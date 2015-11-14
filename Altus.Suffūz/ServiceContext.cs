using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Altus.Suffūz.Windows
{
    public class ServiceContext : ApplicationContext
    {
        private ServiceContext(ServiceBase[] services, params string[] args)
        {
            Services = services;
            Args = args;
            RunAsService = !Args.Contains("console");
        }

        public bool RunAsService { get; private set; }
        public string[] Args { get; private set; }
        public ServiceBase[] Services { get; private set; }

        public static int Run(ServiceBase service, params string[] args)
        {
            return Run(new ServiceBase[] { service }, args);
        }

        public static int Run(ServiceBase[] services, params string[] args)
        {
            var context = new ServiceContext(services, args);
            return context.Run();
        }

        private int Run()
        {
            if (RunAsService)
            {
                ServiceBase.Run(Services);
            }
            else
            {
                foreach (var s in Services)
                {
                    MethodInfo mi = s.GetType().GetMethod("OnStart", BindingFlags.NonPublic | BindingFlags.Instance);
                    mi.Invoke(s, new object[] { Args });
                }
                Application.Run();
            }
            return 0;
        }

        protected override void ExitThreadCore()
        {
            if (!RunAsService)
            {
                foreach (var s in Services)
                {
                    if (s.CanStop) s.Stop();
                    s.Dispose();
                }
            }
            base.ExitThreadCore();
        }
    }
}
