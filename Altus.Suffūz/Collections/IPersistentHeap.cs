using System.Collections;
using System.Collections.Generic;

namespace Altus.Suffūz.Collections
{
    public interface IPersistentHeap : IPersistentCollection
    {
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