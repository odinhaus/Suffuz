using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Collections
{
    public interface IPersistentStack : IPersistentCollection
    {
        void Push(object item);
        object Pop();
        bool TryPop(out object item);
        object Peek();
    }

    public interface IPersistentStack<TValue> : IPersistentStack
    {
        void Push(TValue item);
        new TValue Pop();
        bool TryPop(out TValue item);
        new TValue Peek();
    }
}
