using System.Collections.Generic;

namespace Altus.Suffūz.Collections
{
    public interface IPersistentDictionary<TKey, TValue> : IPersistentHeap<TValue>
    {
        TValue this[TKey key] { get; set; }

        bool AllowOverwrites { get; }
        ICollection<TKey> Keys { get; }
        ICollection<TValue> Values { get; }

        void Add(KeyValuePair<TKey, TValue> item);
        void Add(TKey key, TValue value);
        bool Contains(KeyValuePair<TKey, TValue> item);
        bool ContainsKey(TKey key);
        void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex);
        bool Remove(TKey key);
        bool Remove(KeyValuePair<TKey, TValue> item);
        bool TryGetValue(TKey key, out TValue value);
    }
}