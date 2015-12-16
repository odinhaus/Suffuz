using Altus.Suffūz.Threading;
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
        /// <summary>
        /// The path to the storage file on disk
        /// </summary>
        string BaseFilePath { get; }
        /// <summary>
        /// The maximum capacity of the collection in bytes
        /// </summary>
        int MaximumSize { get; }
        /// <summary>
        /// Removes all items from the collection without triggering a collection compaction
        /// </summary>
        void Clear();
        /// <summary>
        /// Removes all items from the collection, optionally triggering a collection compaction
        /// </summary>
        /// <param name="compact"></param>
        void Clear(bool compact);
        /// <summary>
        /// Increases the MaximumSize of the collection
        /// </summary>
        /// <param name="capacityToAdd"></param>
        void Grow(int capacityToAdd);
        /// <summary>
        /// Reclaims free memory blocks from the collection
        /// </summary>
        void Compact();
        /// <summary>
        /// Indicates whether the instance has been disposed
        /// </summary>
        bool IsDisposed { get; }
        /// <summary>
        /// A locking object used to synchronize access to the collection
        /// </summary>
        ExclusiveLock SyncLock { get; }
    }
}