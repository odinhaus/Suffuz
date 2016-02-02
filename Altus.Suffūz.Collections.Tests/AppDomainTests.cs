using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.IO;

namespace Altus.Suffūz.Collections.Tests
{
    [Serializable]
    [TestClass]
    public class AppDomainTests : MarshalByRefObject
    {
        [TestMethod]
        public void CanDisposeDuringAppDomainUnload()
        {
            var codeBase = Environment.CurrentDirectory;
            var ads = new AppDomainSetup
            {
                DisallowBindingRedirects = false,
                DisallowCodeDownload = false,
                ShadowCopyFiles = "true",
                ApplicationName = "test domain",
                CachePath = Path.Combine(codeBase, "Shadow"),
                ApplicationBase = codeBase,
                PrivateBinPath = Path.Combine(codeBase, "Apps"),
                ShadowCopyDirectories = codeBase + ";" + Path.Combine(codeBase, "Data"),
                ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile,
            };

            var ad = AppDomain.CreateDomain(ads.ApplicationName, null, ads);
            ad.AssemblyResolve += Ad_AssemblyResolve;
            ad.TypeResolve += Ad_TypeResolve;
            var child = (DomainChild)ad.CreateInstanceAndUnwrap(
                typeof(DomainChild).Assembly.FullName,
                typeof(DomainChild).FullName);
            child.Run();

            AppDomain.Unload(ad);


            var ad2 = AppDomain.CreateDomain(ads.ApplicationName, null, ads);
            ad2.AssemblyResolve += Ad_AssemblyResolve;
            ad2.TypeResolve += Ad_TypeResolve;
            var child2 = (DomainChild)ad2.CreateInstanceAndUnwrap(
                typeof(DomainChild).Assembly.FullName,
                typeof(DomainChild).FullName);
            child2.Run();

            AppDomain.Unload(ad2);

        }

        private System.Reflection.Assembly Ad_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            return Assembly.LoadFrom(args.Name);
        }

        private System.Reflection.Assembly Ad_TypeResolve(object sender, ResolveEventArgs args)
        {
            return TypeHelper.GetType(args.Name).Assembly;
        }
    }

    [Serializable]
    public class DomainChild : MarshalByRefObject
    {
        public DomainChild()
        {
        }

        public void Run()
        {
            var manager = new PersistentCollectionManager();
            var dictionary = manager.GetOrCreate<IPersistentDictionary<string, string>>(
                "observables.bin",
                (file) => new PersistentDictionary<string, string>(file, manager.GlobalHeap, false));
            dictionary["foo"] = "fum";
        }
    }
}
