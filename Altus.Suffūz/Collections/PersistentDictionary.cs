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
    public class PersistentDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IPersistentDictionary<TKey, TValue>
    {
        PersistentHeap<KVP> _keys;
        IPersistentHeap _values;
        Dictionary<TKey, KVP> _keyToValueKey;

        public PersistentDictionary() : base()
        {
        }

        public PersistentDictionary(string filePath, int maxSize = 1024 * 1024 * 10, bool allowOverwrites = false)
        {
            AllowOverwrites = allowOverwrites;
            Initialize(true, filePath, maxSize);
        }

        public PersistentDictionary(string indexFilePath, IPersistentHeap valueHeap, bool allowOverwrites = false)
        {
            AllowOverwrites = allowOverwrites;
            _values = valueHeap;
            Initialize(false, indexFilePath, valueHeap.MaximumSize);
        }

        protected void Initialize(bool isNewFile, string filePath, int maxSize)
        {
            OwnsHeap = isNewFile;
            var keyFile = isNewFile ? Path.ChangeExtension(Path.GetFileNameWithoutExtension(filePath) + "_keys", "bin") : filePath;
            _keys = new PersistentHeap<KVP>(keyFile, maxSize);
            if (isNewFile)
            {
                _values = new PersistentHeap<TValue>(filePath, maxSize);
            }
            _keyToValueKey = new Dictionary<TKey, KVP>();

            LoadDictionary();
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

        public object SyncRoot { get { return _values.SyncRoot; } }

        public int Count { get { lock (SyncRoot) { return _keyToValueKey.Count; } } }

        public bool IsReadOnly { get { return false; } }

        public int AutoGrowSize { get; set; }

        public bool CompactBeforeGrow { get; set; }

        public string BaseFilePath { get; private set; }

        public int MaximumSize { get { return _values.MaximumSize; } }

        public bool IsSynchronized { get { return true; } }

        public int Length { get { return _values.Length; } }

        public bool OwnsHeap { get; private set; }

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
                var keyKey = _keys.Add(kvp);
                kvp.KeyKey = keyKey;
                var valueKey = Write(value);
                kvp.ValueKey = valueKey;
                _keys.WriteUnsafe(kvp, keyKey);
                _keyToValueKey.Add(key, kvp);
            }
        }

        public virtual void Clear()
        {
            Clear(false);
        }

        public virtual void Clear(bool compact)
        {
            if (OwnsHeap)
            {
                _values.Clear(compact);
            }
            else
            {
                foreach (var key in _keyToValueKey.Values)
                {
                    Free(key.ValueKey);
                }
            }

            _keys.Clear(compact);
            _keyToValueKey.Clear();
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

        

        public virtual void Compact()
        {
            if (_values != null)
            {
                _values.Compact();
            }
            if (_keys != null)
            {
                _keys.Compact();
            }
        }

        public virtual void Flush()
        {
            if (_values != null)
            {
                _values.Flush();
            }
            if (_keys != null)
            {
                _keys.Flush();
            }
        }

        public void Grow(int capacityToAdd)
        {
            _values.Grow(capacityToAdd);
            _keys.Grow(capacityToAdd);
        }

        public void CopyTo(Array array, int index)
        {
            var i = 0;
            foreach(var kvp in this)
            {
                array.SetValue(kvp, index + i);
            }
        }

        protected virtual TValue Read(ulong key)
        {
            return _values.Read<TValue>(key);
        }

        protected virtual ulong OverwriteUnsafe(TValue value, ulong key)
        {
            return _values.WriteUnsafe(value, key);
        }

        protected virtual ulong Write(TValue value)
        {
            return _values.Add(value);
        }

        protected virtual void Free(ulong key)
        {
            _values.Free(key);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            var en = _keyToValueKey.GetEnumerator();
            while(en.MoveNext())
            {
                yield return new KeyValuePair<TKey, TValue>(en.Current.Key, Read(en.Current.Value.ValueKey));
            }
        }

        public bool IsDisposed { get { return disposed; } }

        #region IDisposable Members
        bool disposed = false;

        // Implement IDisposable.
        // Do not make this method virtual.
        // A derived class should not be able to override this method.
        public void Dispose()
        {
            Dispose(true);
            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SupressFinalize to
            // take this object off the finalization queue 
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        public event EventHandler Disposing;
        public event EventHandler Disposed;
        //========================================================================================================//
        // Dispose(bool disposing) executes in two distinct scenarios.
        // If disposing equals true, the method has been called directly
        // or indirectly by a user's code. Managed and unmanaged resources
        // can be disposed.
        // If disposing equals false, the method has been called by the 
        // runtime from inside the finalizer and you should not reference 
        // other objects. Only unmanaged resources can be disposed.
        private void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!this.disposed)
            {
                if (this.Disposing != null)
                    this.Disposing(this, new EventArgs());
                // If disposing equals true, dispose all managed 
                // and unmanaged resources.
                if (disposing)
                {
                    // Dispose subclass managed resources.
                    this.OnDisposeManagedResources();
                }

                // Call the appropriate methods to clean up 
                // unmanaged resources here.
                // If disposing is false, 
                // only the following code is executed.
                this.OnDisposeUnmanagedResources();
                if (this.Disposed != null)
                    this.Disposed(this, new EventArgs());
            }
            disposed = true;
        }

        /// <summary>
        /// Dispose unmanaged (native resources)
        /// </summary>
        protected virtual void OnDisposeUnmanagedResources()
        {
        }
        protected virtual void OnDisposeManagedResources()
        {
            if (_keys != null)
            {
                _keys.Dispose();
                _keys = null;
            }

            if (_values != null)
            {
                _values.Dispose();
                _values = null;
            }

            if (_keyToValueKey != null)
            {
                _keyToValueKey.Clear();
                _keyToValueKey = null;
            }
        }
        #endregion




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
