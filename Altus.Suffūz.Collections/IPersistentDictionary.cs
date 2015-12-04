using System.Collections.Generic;

namespace Altus.Suffūz.Collections
{
    public interface IPersistentDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IPersistentCollection
    {
        /// <summary>
        /// Indicates whether assign item values using the indexer will allow current items with the same key to be overwritten, or freed with new items 
        /// appended to the collection's heap.  Setting this value to True should only be done if you can be certain the serialized size of the items being persisted 
        /// will always be the same.
        /// </summary>
        bool AllowOverwrites { get; }
    }
}