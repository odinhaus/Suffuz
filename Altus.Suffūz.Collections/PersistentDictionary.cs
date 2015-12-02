using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Collections
{
    public class PersistentDictionary<TKey, TValue> : PersistentHeap<TValue>, IDictionary<TKey, TValue>
    {
        PersistentHeap<KVP> _keys;
        Dictionary<TKey, KVP> _keyToValueKey;

        public PersistentDictionary() : base()
        {
        }

        public PersistentDictionary(string filePath, int maxSize = 1024 * 1024 * 10, bool allowOverwrites = false) : base(filePath, maxSize)
        {
            AllowOverwrites = allowOverwrites;
        }

        protected override void Initialize(bool isNewFile, string filePath, int maxSize)
        {
            var keyFile = Path.GetFileNameWithoutExtension(filePath) + "_keys";
            _keys = new PersistentHeap<KVP>(Path.ChangeExtension(keyFile, "bin"), maxSize);
            _keyToValueKey = new Dictionary<TKey, KVP>();

            LoadDictionary();
            base.Initialize(isNewFile, filePath, maxSize);
        }

        private void LoadDictionary()
        {
            lock(SyncRoot)
            {
                foreach (var kvp in _keys)
                {
                    _keyToValueKey.Add(kvp.Key, kvp);
                }
            }
        }

        public TValue this[TKey key]
        {
            get
            {
                lock(SyncRoot)
                {
                    if (ContainsKey(key))
                    {
                        return Read(_keyToValueKey[key].ValueKey);
                    }
                    else
                    {
                        throw new KeyNotFoundException("The key was not found.");
                    }
                }
            }
            set
            {
                lock(SyncRoot)
                {
                    KVP kvp;
                    if (_keyToValueKey.TryGetValue(key, out kvp))
                    {
                        if (AllowOverwrites)
                        {
                            OverwriteUnsafe(value, kvp.ValueKey);
                        }
                        else
                        {
                            Remove(key);
                            Add(key, value);
                        }
                    }
                    else
                    {
                        Add(key, value);
                    }
                }
            }
        }

        public ICollection<TKey> Keys
        {
            get
            {
                lock(SyncRoot)
                {
                    return _keyToValueKey.Keys;
                }
            }
        }

        public ICollection<TValue> Values
        {
            get
            {
                lock (SyncRoot)
                {
                    return _keyToValueKey.Values.Select(k => Read(k.ValueKey)).ToList();
                }
            }
        }

        /// <summary>
        /// Setting this value to True will allow the dictionary to overwrite existing entries in their current memory locations, rather than invalidating entries and creating new 
        /// entries at the next free space in the heap.  For TValue types of FIXED SIZE, this can result in less heap fragmentation and smaller heap allocations.  This setting 
        /// should ALWAYS be False for TValue types whose serialized size can vary from instance to instance.  This value is False by default.
        /// </summary>
        public bool AllowOverwrites { get; private set; }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        public void Add(TKey key, TValue value)
        {
            if (ContainsKey(key))
            {
                throw new InvalidOperationException("The key already exists.");
            }

            lock(SyncRoot)
            {
                var kvp = new KVP()
                {
                    Key = key
                };
                var keyKey = _keys.Write(kvp);
                kvp.KeyKey = keyKey;
                var valueKey = Write(value);
                kvp.ValueKey = valueKey;
                _keys.OverwriteUnsafe(kvp, keyKey);
                _keyToValueKey.Add(key, kvp);
            }
        }

        public override void Clear()
        {
            using (var scope = new FlushScope())
            {
                _keys.Clear();
                base.Clear();
            }
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            KVP kvp;
            lock(SyncRoot)
            {
                if (_keyToValueKey.TryGetValue(item.Key, out kvp))
                {
                    return Read(kvp.ValueKey).Equals(item.Value);
                }
            }
            return false;
        }

        public bool ContainsKey(TKey key)
        {
            lock(SyncRoot)
            {
                return _keyToValueKey.ContainsKey(key);
            }
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            var i = 0;
            foreach(var x in ((IEnumerable < KeyValuePair < TKey, TValue >> )this))
            {
                array[arrayIndex + i] = (KeyValuePair<TKey, TValue>)x;
            }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return Remove(item.Key);
        }

        public bool Remove(TKey key)
        {
            KVP kvp;
            lock (SyncRoot)
            {
                if (_keyToValueKey.TryGetValue(key, out kvp))
                {
                    Free(kvp.ValueKey);
                    _keys.Free(kvp.KeyKey);
                    _keyToValueKey.Remove(key);
                }
            }
            return false;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            KVP kvp;
            value = default(TValue);
            lock (SyncRoot)
            {
                if (_keyToValueKey.TryGetValue(key, out kvp))
                {
                    value = Read(kvp.ValueKey);
                    return true;
                }
            }
            return false;
        }

        public override void Compact()
        {
            base.Compact();
            if (_keys != null)
            {
                _keys.Compact();
            }
        }

        public override void Flush()
        {
            base.Flush();
            if (_keys != null)
            {
                _keys.Flush();
            }
        }

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
            var en = _keyToValueKey.GetEnumerator();
            while(en.MoveNext())
            {
                yield return new KeyValuePair<TKey, TValue>(en.Current.Key, Read(en.Current.Value.ValueKey));
            }
        }

        protected override void OnDisposeManagedResources()
        {
            if (_keys != null)
            {
                _keys.Dispose();
                _keys = null;
            }

            if (_keyToValueKey != null)
            {
                _keyToValueKey.Clear();
                _keyToValueKey = null;
            }

            base.OnDisposeManagedResources();
        }

        public class KVP
        {
            [BinarySerializable(0)]
            public TKey Key { get; set; }
            [BinarySerializable(1)]
            public ulong ValueKey { get; set; }
            [BinarySerializable(2)]
            public ulong KeyKey { get; set; }
        }
    }
}
