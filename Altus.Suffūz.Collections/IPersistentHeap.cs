using System.Collections;
using System.Collections.Generic;

namespace Altus.Suffūz.Collections
{
    public interface IPersistentHeap : IPersistentCollection
    {
        IEnumerable<ulong> AllKeys { get; }
        int Length { get; }
        bool Contains(object item, out ulong key);
        void Free(ulong key);
        ulong OverwriteUnsafe(object item, ulong key);
        object Read(ulong key);
        TValue Read<TValue>(ulong key);
        ulong Write(object item);
        bool IsReadOnly { get; }
    }

    public interface IPersistentHeap<TValue> : IPersistentHeap, ICollection<TValue>, IEnumerable<TValue>
    {
        new IEnumerator<TValue> GetEnumerator();
        ulong OverwriteUnsafe(TValue item, ulong key);
        new TValue Read(ulong key);
        ulong Write(TValue item);
    }
}