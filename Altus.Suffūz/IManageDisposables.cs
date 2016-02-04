using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz
{
    public interface IManageDisposables : IDisposable
    {
        void Add(IDisposable disposable);
        void Remove(IDisposable disposable);
    }
}
