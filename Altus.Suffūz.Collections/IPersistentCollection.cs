using System;
using System.Collections;

namespace Altus.Suffūz.Collections
{
    public interface IPersistentCollection : ICollection, IEnumerable, IDisposable, IFlush
    {
        event EventHandler Disposed;
        event EventHandler Disposing;

        /// <summary>
        /// Setting this to a non-zero value allows the collection to automatically grow in size when an OutOfMemory condition occurs.
        /// </summary>
        int AutoGrowSize { get; set; }
        /// <summary>
        /// Setting this value to true will cause the collection to be compacted first before it is Grown.
        /// </summary>
        bool CompactBeforeGrow { get; set; }
        /// <summary>
        /// The size of the collection on disk
        /// </summary>
        int Length { get; }
        string FilePath { get; }
        int MaximumSize { get; }
        void Clear();
        void Clear(bool compact);
        void Grow(int capacityToAdd);
        void Compact();
    }
}