using System.Collections;
using System.Collections.Generic;

namespace Altus.Suffūz.Collections
{
    public interface IPersistentHeap : IPersistentCollection
    {
        /// <summary>
        /// Adds a shared resource to a common collection, allowing entries for that resource to be indexed when data is written to the heap.  
        /// Sharing heaps reduces the number of individual storage files used by persistent types, which can significantly improve 
        /// performance.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <exception cref="System.InvalidOperationException">The system supports a maximum 4096 shares per heap</exception>
        /// <returns>The internal numeric index for the share</returns>
        ushort AddShare(string resourceName);
        /// <summary>
        /// Removes the share, and all of its associated data from the heap, if found, and returns true.  Otherwise, returns false.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        bool RemoveShare(string resourceName);
        /// <summary>
        /// Returns all the location keys for each item in the collection
        /// </summary>
        IEnumerable<ulong> AllKeys { get; }
        /// <summary>
        /// returns true if the item exists in the collection by evaluating the Equals() method on the supplied item 
        /// against those items currently in the collection
        /// </summary>
        /// <param name="item"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        bool Contains(object item, out ulong key);
        /// <summary>
        /// Marks an item in the collection as being free to discard during a collection compaction
        /// </summary>
        /// <param name="key"></param>
        void Free(ulong key);
        /// <summary>
        /// Attempts to write the item at the key location provided.  Overwriting will succeed if the length of the current item 
        /// matches the length of the item already stored and if they are of the same type or the current item has been freed.  If the items differ in the length, 
        /// or the stored item is valid but of a different type, then the write operation will be appended to the top of the heap, and new key will be returned.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        ulong Write(object item, ulong key);
        /// <summary>
        /// Overwrites the item at the current location key without bounds or type checking.  Use this only if you are certain the item you are writing is the same length 
        /// as the item already on disk, or the heap will be corrupted.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        ulong WriteUnsafe(object item, ulong key);
        /// <summary>
        /// Reads an item from the heap with the given location key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        object Read(ulong key);
        /// <summary>
        /// Reads an item from the heap with the given location key
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        TValue Read<TValue>(ulong key);
        /// <summary>
        /// Adds an item to top of the heap and returns its location key
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        ulong Add(object item);
        /// <summary>
        /// Indicates whether the heap can be written to
        /// </summary>
        bool IsReadOnly { get; }
        /// <summary>
        /// The next incremental sequence number to assign to a newly added item to the heap
        /// </summary>
        ulong HeapSequenceNumber { get; }
        /// <summary>
        /// Gets an actual memory location for the provided key, if found, otherwise returns -1.  
        /// NOTE: addresses can change, so take care when persisting these values outside of a locked read/write context.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        int GetAddress(ulong key);
    }

    public interface IPersistentHeap<TValue> : IPersistentHeap, ICollection<TValue>, IEnumerable<TValue>
    {
        /// <summary>
        /// Attempts to write the item at the key location provided.  Overwriting will succeed if the length of the current item 
        /// matches the length of the item already stored and if they are of the same type or the current item has been freed.  If the items differ in the length, 
        /// or the stored item is valid but of a different type, then the write operation will be appended to the top of the heap, and new key will be returned.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        ulong Write(TValue item, ulong key);
        /// <summary>
        /// Overwrites the item at the current location key without bounds or type checking.  Use this only if you are certain the item you are writing is the same length 
        /// as the item already on disk, or the heap will be corrupted.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        ulong WriteUnsafe(TValue item, ulong key);
        /// <summary>
        /// Reads an item from the heap with the given location key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        new TValue Read(ulong key);
        /// <summary>
        /// Adds an item to top of the heap and returns its location key
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        new ulong Add(TValue item);
    }
}