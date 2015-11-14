using Altus.Suffūz.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz
{
    public interface IBootstrapper
    {
        IResolveTypes Initialize();
        string InstanceName { get; }
        ulong InstanceId { get; }
        byte[] InstanceCryptoKey { get; }
    }
}
