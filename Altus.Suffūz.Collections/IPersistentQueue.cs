using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Collections
{
    public interface IPersistentQueue : IPersistentCollection
    {
        void Enqueue(object item);
        object Dequeue();
        bool TryDequeue(out object item);
        object Peek();
    }

    public interface IPersistentQueue<TValue> : IPersistentQueue
    {
        void Enqueue(TValue item);
        new TValue Dequeue();
        bool TryDequeue(out TValue item);
        new TValue Peek();
    }
}
