using System;
using System.Collections;

namespace Altus.Suffūz.Collections
{
    public interface IPersistentCollection : ICollection, IEnumerable, IDisposable, IFlush
    {
        event EventHandler Disposed;
        event EventHandler Disposing;

        string FilePath { get; }
        int MaximumSize { get; }
        void Clear();
        void Clear(bool compact);
        void Grow(int capacityToAdd);
    }
}