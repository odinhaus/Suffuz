using Altus.Suffūz.Collections.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using Altus.Suffūz.Serialization.Binary;
using System.Threading;
using Altus.Suffūz.Serialization;
using Altus.Suffūz.IO;
using System.Security.Cryptography;
using Altus.Suffūz.Diagnostics;
using System.Transactions;

namespace Altus.Suffūz.Collections
{
    public enum WALEntryType : int
    {
        ShareUpdate = 1,
        ShareHeaderUpdate = 2,
        RecordUpdate = 20,
        RecordHeaderUpdate = 21,
        Checkpoint = 30,
        TXBegin = 40,
        TXCommit = 41,
        TXRollback = 42,
        TXNewValue = 43,
        TXOldValue = 44
    }

    public class PersistentHeap<TValue> : PersistentHeap, ICollection<TValue>, IEnumerable<TValue>, IPersistentHeap<TValue>
    {
        public PersistentHeap() : base()
        {
        }

        public PersistentHeap(string filePath, int maxSize = 1024 * 1024 * 1024) : base(filePath, maxSize)
        {

        }

        protected override void CheckTypeTable(Type type)
        {
            if (type != typeof(TValue))
                throw new InvalidCastException(string.Format("The type {0} is not supported by this Heap.", type.Name));
        }

        public virtual ulong Add(TValue item)
        {
            return base.Add(item);
        }

        public virtual ulong Write(TValue item, ulong key)
        {
            return base.Write(item, key);
        }

        public virtual ulong WriteUnsafe(TValue item, ulong key)
        {
            return base.WriteUnsafe(item, key);
        }

        public virtual new TValue Read(ulong key)
        {
            return base.Read<TValue>(key);
        }

        protected override Type GetCodeType(int code)
        {
            return typeof(TValue);
        }

        protected override int GetTypeCode(Type type)
        {
            return 1;
        }

        ISerializer _serializer = null;
        protected override ISerializer GetSerializer(Type itemType)
        {
            if (_serializer == null)
            {
                _serializer = base.GetSerializer(itemType);
            }
            return _serializer;
        }

        public new IEnumerator<TValue> GetEnumerator()
        {
            var en = base.GetEnumerator();
            while(en.MoveNext())
            {
                yield return (TValue)en.Current;
            }
        }

        void ICollection<TValue>.Add(TValue item)
        {
            this.Add(item);
        }

        public bool Contains(TValue item)
        {
            ulong key;
            return base.Contains(item, out key);
        }

        public void CopyTo(TValue[] array, int arrayIndex)
        {
            var i = 0;
            foreach(var x in this)
            {
                array[arrayIndex + i] = x;
                i++;
            }
        }

        bool ICollection<TValue>.Remove(TValue item)
        {
            ulong key;
            if (base.Contains(item, out key))
            {
                base.Free(key);
                return true;
            }
            return false;
        }
    }

    public unsafe class PersistentHeap : PersistentCollection, IPersistentHeap
    {
        protected const int SHARED_HEADER_LENGTH = 1024 * 64;
        protected const int SHARED_HEADER_INDEX_LENGTH = 16;
        protected const int SHARED_HEADER_NEXT = 0;
        protected const int SHARED_HEADER_NEXT_LENGTH = 2;

        protected int HEAP_HEAD_ROOM;
        protected const int HEAP_HEADER_LENGTH = SHARED_HEADER_LENGTH + 4 + 4 + 4 + 8;
        protected const int HEAP_NEXT = 8;
        protected const int HEAP_FIRST = 0;
        protected const int HEAP_LAST = 4;
        protected const int HEAP_SEQUENCE = 12;

        protected const int ITEM_ISVALID = 0;
        protected const int ITEM_INDEX = 1;
        protected const int ITEM_LENGTH = 9;
        protected const int ITEM_TYPE = 13;
        protected const int ITEM_DATA = 17;



        static int COUNTER = 0;
        static object GlobalSyncRoot = new object();

        bool _isInitialized = false;
        private byte* _filePtr;
        Dictionary<ulong, int> _addresses = new Dictionary<ulong, int>();
        Dictionary<int, ulong> _keys = new Dictionary<int, ulong>();
        static Dictionary<int, Type> _typesByCode = new Dictionary<int, Type>();
        static Dictionary<Type, int> _codesByType = new Dictionary<Type, int>();

        /// <summary>
        /// Create a new heap using a system generated file location, and DEFAULT_HEAP_SIZE (10Mb)
        /// </summary>
        public PersistentHeap() : base()
        {
        }
        /// <summary>
        /// Create a new heap using a supplied file path and optional maximum capacity
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="maxSize"></param>
        public PersistentHeap(string filePath, int maxSize = DEFAULT_HEAP_SIZE, bool isAtomic = true) 
            : base(filePath, maxSize + HEAP_HEADER_LENGTH, isAtomic)
        {
            Interlocked.Increment(ref COUNTER);
        }

        public override int Count
        {
            get
            {
                lock(SyncRoot)
                {
                    return _addresses.Count;
                }
            }
        }

        public virtual IEnumerable<ulong> AllKeys
        {
            get
            {
                lock (SyncRoot)
                {
                    return _addresses.Keys;
                }
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }


        protected MemoryMappedViewAccessor MMVA { get; private set; }
        protected static FileStream TypesFile { get; private set; }
        protected ushort NextShare { get; private set; }

        protected Dictionary<ulong, int> Addresses {  get { return _addresses; } }

        public ulong HeapSequenceNumber { get; private set; }

        public virtual ushort AddShare(string resourceName)
        {
            if ((NextShare - SHARED_HEADER_NEXT_LENGTH) < 1024 * 63)
            {
                var hashed = MD5.Create().ComputeHash(UTF8Encoding.UTF8.GetBytes(resourceName));
                using (var ptr = CreatePointerAdapter())
                {
                    ptr.Write(NextShare, hashed);
                    NextShare += 16;
                    ptr.Write(0, NextShare);
                }
                return NextShare;
            }
            else
            {
                throw new InvalidOperationException("A single heap may only be shared 4096 resources, or less");
            }
        }

        public virtual bool RemoveShare(string resourceName)
        {
            return false;
        }

        protected virtual BytePointerAdapter CreatePointerAdapter()
        {
            if (IsAtomic)
                return TransactedPointerAdapter.Create(ref _filePtr, 0, ViewSize, this);
            else
                return new BytePointerAdapter(ref _filePtr, 0, ViewSize);
        }

        /// <summary>
        /// Frees all items in the heap without compacting the heap
        /// </summary>
        public override void Clear()
        {
            Clear(false);
        }

        /// <summary>
        /// Frees all items in the heap, optionally compacting the heap when done
        /// </summary>
        /// <param name="compact"></param>
        public override void Clear(bool compact)
        {
            if (compact)
            {
                OnDisposeManagedResources();
                // just delete the File and start over
                System.IO.File.Delete(BaseFilePath);
                First = Next = Last = 0;
                Initialize(BaseFilePath, MaximumSize);
            }
            else
            {
                using (var scope = new FlushScope())
                {
                    foreach (var key in AllKeys.ToArray())
                    {
                        Free(key);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the storage address for the given key, if it exists, otherwise returns -1.  
        /// NOTE: storage addresses can change, so be careful when persisting these values.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public int GetAddress(ulong key)
        {
            int address;
            lock(SyncRoot)
            {
                if (Addresses.TryGetValue(key, out address))
                {
                    return address;
                }
            }
            return -1;
        }

        /// <summary>
        /// Writes the item into the next available free location on the heap, and returns a key for that location which can be used for 
        /// subsequent Read, Free and OverwriteUnsafe calls.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public virtual ulong Add(object item)
        {
            lock (SyncRoot)
            {
                var itemType = item.GetType();
                CheckTypeTable(itemType);

                if (Next == HEAP_HEADER_LENGTH)
                {
                    First = Last = Next;
                }
                else
                {
                    CheckHeadRoom();
                }

                HeapSequenceNumber++;
                
                _addresses.Add(HeapSequenceNumber, Next);
                _keys.Add(Next, HeapSequenceNumber);

                using (var ptr = CreatePointerAdapter())
                {
                    using (var scope = new FlushScope())
                    {
                        scope.Enlist(this);
                        var bytes = ptr.Write(Next, CreateItemRecord(item, HeapSequenceNumber, true));
                        Last = Next;
                        Next += ITEM_DATA + bytes;
                        UpdateHeaders();
                    }
                }
                
                return HeapSequenceNumber;
            }
        }

        protected virtual byte[] CreateItemRecord(object item, ulong key, bool isValid)
        {
            lock(SyncRoot)
            {
                var bytes = isValid ? GetSerializer(item.GetType()).Serialize(item) : new byte[0];
                var data = new byte[ITEM_DATA + bytes.Length];
                var itemType = isValid ? item.GetType() : typeof(object);
                CheckTypeTable(itemType);
                var typeCode = GetTypeCode(itemType);

                fixed (byte* dataPtr = data)
                {
                    byte* p = dataPtr;
                    *(bool*)p = isValid;
                    p++;
                    *(ulong*)p = key;
                    p += 8;
                    *(int*)p = bytes.Length;
                    p += 4;
                    *(int*)p = typeCode;
                    p += 4;
                }

                Buffer.BlockCopy(bytes, 0, data, ITEM_DATA, bytes.Length);
                return data;
            }
        }

        /// <summary>
        /// Allows the caller to overwrite the address pointed to by key with item.  This call does not do any bounds checking, so 
        /// it is imperative that the caller is certain that the serialized size of item is exactly the same as the existing item at key, 
        /// otherwise this call can corrupt the heap.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public virtual ulong WriteUnsafe(object item, ulong key)
        {
            var address = _addresses[key];

            lock (SyncRoot)
            {
                using (var ptr = CreatePointerAdapter())
                {
                    using (var scope = new FlushScope())
                    {
                        scope.Enlist(this);
                        ptr.Write(address, CreateItemRecord(item, key, true));
                    }
                }
                return key;
            }
        }

        public override void WriteUnsafe(int address, byte[] data)
        {
            // transaction pointer adapter will call back here when it needs to reset a data value during rollback
            // this gives us a chance to examine the data, and keep our memory keys in sync
            lock(SyncRoot)
            {
                if (address > HEAP_HEADER_LENGTH)
                {
                    // this is a data record
                    bool isValid = BitConverter.ToBoolean(data, ITEM_ISVALID);
                    ulong key = BitConverter.ToUInt64(data, ITEM_INDEX);

                    if (isValid)
                    {
                        _addresses[key] = address;
                        _keys[address] = key;
                    }
                    else
                    {
                        if (_keys.TryGetValue(address, out key))
                        {
                            _addresses.Remove(key);
                            _keys.Remove(address);
                        }
                    }

                    using (var ptr = new BytePointerAdapter(ref _filePtr, 0, ViewSize))
                    {
                        // we don't want this in a transaction
                        ptr.Write(address + ITEM_ISVALID, data);
                    }
                }
                else
                {
                    // this is a header
                    switch(address)
                    {
                        case HEAP_FIRST:
                            First = BitConverter.ToInt32(data, 0);
                            break;
                        case HEAP_LAST:
                            Last = BitConverter.ToInt32(data, 0);
                            break;
                        case HEAP_NEXT:
                            Next = BitConverter.ToInt32(data, 0);
                            break;
                        case HEAP_SEQUENCE:
                            HeapSequenceNumber = BitConverter.ToUInt64(data, 0);
                            break;
                    }
                }
            }
        }

        public virtual ulong Write(object item, ulong key)
        {
            var address = _addresses[key];

            lock (SyncRoot)
            {
                using (var ptr = CreatePointerAdapter())
                {
                    var length = ptr.ReadInt32(address + ITEM_LENGTH);
                    var bytes = CreateItemRecord(item, key, true);
                    using (var scope = new FlushScope())
                    {
                        scope.Enlist(this);
                        if (length == bytes.Length)
                        {
                            ptr.Write(address, bytes);
                        }
                        else
                        {
                            Free(key);
                            key = Add(item);
                        }
                    }
                }
                return key;
            }
        }

        /// <summary>
        /// Reads an item from the heap at the location given by key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public virtual object Read(ulong key)
        {
            object value;
            ulong foundKey;
            int nextAddress;
            if (TryRead(_addresses[key], out value, out foundKey, out nextAddress)
                && foundKey == key)
            {
                return value;
            }
            return null;
        }

        protected virtual bool TryRead(int address, out object value, out ulong key, out int nextAddress)
        {
            byte[] bytes;
            Type itemType;
            key = 0;
            value = null;
            nextAddress = address;
            try
            {
                lock (SyncRoot)
                {
                    using (var ptr = CreatePointerAdapter())
                    {
                        var isValid = ptr.ReadBoolean(address + ITEM_ISVALID);
                        if (isValid)
                        {
                            key = ptr.ReadUInt64(address + ITEM_INDEX);
                            var len = ptr.ReadInt32(address + ITEM_LENGTH);
                            bytes = ptr.ReadBytes(address + ITEM_DATA, len);
                            itemType = GetCodeType(ptr.ReadInt32(address + ITEM_TYPE));
                            nextAddress = address + HEAP_HEADER_LENGTH + len;
                        }
                        else return false;
                    }
                }
                value = GetSerializer(itemType).Deserialize(bytes, itemType);
                return true;
            }
            catch
            {
                nextAddress = address;
                return false;
            }
        }

        /// <summary>
        /// Reads an item from the heap at the location given by key
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public virtual TValue Read<TValue>(ulong key)
        {
            return (TValue)Read(key);
        }

        /// <summary>
        /// Marks the item at key as being free to be compacted.  Prevents future reading of the item from the heap, but does not 
        /// actually remove the item from the heap allocation.  Only Compact() will release the freed locations on the heap.
        /// </summary>
        /// <param name="key"></param>
        public virtual void Free(ulong key)
        {
            lock(SyncRoot)
            {
                var address = _addresses[key];

                using (var scope = new FlushScope())
                {
                    scope.Enlist(this);

                    using (var ptr = CreatePointerAdapter())
                    {
                        ptr.Write(address, CreateItemRecord(null, key, false));
                    }
                }

                if (address == First)
                {
                    var newFirst = GetNext(address, true);
                    if (newFirst == -1)
                        newFirst = 0;
                    if (First == Last)
                        First = Last = newFirst;
                    else
                        First = newFirst;
                }
                _addresses.Remove(key);
                _keys.Remove(address);
            }
        }

        /// <summary>
        /// Reclaims and compresses the heap to include only those items that have not been freed.  This will also shrink the size of the 
        /// heap on disk in 64kb chunks.
        /// </summary>
        public override void Compact()
        {
            lock(SyncRoot)
            {
                if (Count > 0)
                {
                    var end = MMVA.Capacity;
                    var address = HEAP_HEADER_LENGTH;
                    var delta = 0;
                    var block = 0;
                    First = Last = 0;
                    using (var ptr = CreatePointerAdapter())
                    {
                        using (var scope = new FlushScope())
                        {
                            scope.Enlist(this);
                            while (address != -1)
                            {
                                address = GetNext(address, false); // first deleted block address
                                if (address > 0)
                                {
                                    // get next valid address after deleted address
                                    var nextValidStart = GetNext(address, true);
                                    if (nextValidStart == -1)
                                    {
                                        // if -1, we don't have any valid addresses,
                                        // so there's no data copying to do
                                        // just wipe it
                                        // but we need to include the length of the final item
                                        Next = address;
                                        break;
                                    }
                                    else
                                    {
                                        // get next invalid address after next valid address
                                        var nextInvalidStart = GetNext(nextValidStart, false);
                                        if (nextInvalidStart == -1)
                                        {
                                            // we're compacted all the way to the end, so set to Capacity
                                            nextInvalidStart = Next;
                                        }

                                        delta = nextValidStart - address; // distance block will move
                                        block = nextInvalidStart - nextValidStart; // length of block to move
                                                                                   // now move the block up
                                        for (int i = 0; i < block; i++)
                                        {
                                            ptr.Write(address + i, ptr.ReadByte(address + delta + i));
                                        }
                                        // invalidate delta block
                                        ptr.Write(address + block + ITEM_ISVALID, false);
                                        ptr.Write(address + block + ITEM_INDEX, (ulong)0);
                                        ptr.Write(address + block + ITEM_TYPE, 0);
                                        ptr.Write(address + block + ITEM_LENGTH, delta - ITEM_DATA);
                                        // repeat, now with valid data at address
                                    }
                                }
                            }
                        }
                    }
                    // wipe free space at end of file
                    SparseFile.SetZero(BaseFile, Next, BaseFile.Length - 1);
                    // update new index values
                    LoadIndices();
                    UpdateHeaders();
                }
                else if (Next > HEAP_HEADER_LENGTH)
                {
                    Clear(true); // this will just kill the file and start over
                }
            }
        }

        public virtual bool Contains(object item, out ulong key)
        {
            var en = _addresses.GetEnumerator();
            while(en.MoveNext())
            {
                if (en.Current.Value.Equals(item))
                {
                    key = en.Current.Key;
                    return true;
                }
            }

            key = 0;
            return false;
        }

        /// <summary>
        /// Enumerates all valid items in the heap.
        /// </summary>
        /// <returns></returns>
        public override IEnumerator GetEnumerator()
        {
            lock(SyncRoot)
            {
                foreach (var k in _addresses)
                {
                    yield return Read(k.Key);
                }
            }
        }

        /// <summary>
        /// Forces the heap to flush unwritten contents to disk.
        /// </summary>
        public override void Flush()
        {
            if (MMVA != null && Next > HEAP_HEADER_LENGTH)
            {
                lock (SyncRoot)
                {
                    MMVA.Flush();
                }
            }
        }

        protected virtual int GetTypeCode(Type type)
        {
            if (type.Implements<ISerializer>() 
                && type.Assembly.IsDynamic)
            {
                type = type.BaseType;
            }

            return _codesByType[type];
        }

        protected virtual Type GetCodeType(int code)
        {
            return _typesByCode[code];
        }






        protected override void Initialize(bool isNewFile, string filePath, int maxSize)
        {
            if (!_isInitialized)
            {
                HEAP_HEAD_ROOM = (int)((float)maxSize * 0.2f);
                lock (GlobalSyncRoot)
                {
                    if (TypesFile == null)
                    {
                        TypesFile = new FileStream("Type_List.bin", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                        LoadTypes();
                    }
                }

                CreateView();
                CheckConsistency();
                ReadHeaders();
                LoadIndices();
                UpdateHeaders();
                CheckHeadRoom();

                _isInitialized = true;
            }
        }

        public virtual void CheckConsistency()
        {
            TransactedPointerAdapter.CheckConsistency(ref _filePtr, 0, MMVA.Capacity, this);
        }

        protected static void LoadTypes()
        {
            lock(GlobalSyncRoot)
            {
                TypesFile.Seek(0, SeekOrigin.Begin);
                _typesByCode.Clear();
                _codesByType.Clear();
                using (var sr = new StreamReader(TypesFile, Encoding.UTF8, true, 4096, true))
                {
                    int index = 1;
                    while (!sr.EndOfStream)
                    {
                        var type = TypeHelper.GetType(sr.ReadLine());
                        _typesByCode.Add(index, type);
                        _codesByType.Add(type, index);
                        index++;
                    }
                }
            }
        }

        protected virtual void CreateView()
        {
            MMVA = BaseMMF.CreateViewAccessor(0, ViewSize);
            _filePtr = MMVA.Pointer(0);
        }

        protected long ViewSize
        {
            get
            {
                return Math.Min(Next + HEAP_HEAD_ROOM + HEAP_HEADER_LENGTH, BaseFile.Length);
            }
        }

        protected virtual void LoadIndices()
        {
            var address = HEAP_HEADER_LENGTH;
            ulong key;
            _addresses.Clear();
            _keys.Clear();
            while(address < Next)
            {
                using (var ptr = CreatePointerAdapter())
                {
                    key = ptr.ReadUInt64(address + 1);
                    if (ptr.ReadBoolean(address))
                    {
                        if (First == 0)
                        {
                            First = address;
                        }
                        Last = address;
                        _addresses.Add(key, address);
                        _keys.Add(address, key);
                    }
                    address += ITEM_DATA + ptr.ReadInt32(address + ITEM_LENGTH);
                }
            }
        }

        protected virtual int GetNext(int address, bool isValid)
        {
            lock(SyncRoot)
            {
                using (var ptr = CreatePointerAdapter())
                {
                    var currentValid = ptr.ReadBoolean(address);
                    while (currentValid != isValid && address < Next)
                    {
                        var len = ptr.ReadInt32(address + ITEM_LENGTH);
                        address += ITEM_DATA + len;
                        currentValid = ptr.ReadBoolean(address);
                    }
                }
            }
            if (address < Next)
                return address;
            else return -1;
        }

        protected virtual void UpdateHeaders()
        {
            lock (SyncRoot)
            {
                using (var ptr = CreatePointerAdapter())
                {
                    ptr.Write(0, First);
                    ptr.Write(4, Last);
                    ptr.Write(8, Next);
                    ptr.Write(12, HeapSequenceNumber);
                }
            }
        }

        protected virtual void ReadHeaders()
        {
            lock (SyncRoot)
            {
                BaseFile.Seek(0, SeekOrigin.Begin);
                First = BaseFile.ReadInt32();
                Last = BaseFile.ReadInt32();
                Next = BaseFile.ReadInt32();
                HeapSequenceNumber = BaseFile.ReadUInt64();
                BaseFile.Seek(0, SeekOrigin.Begin);
                if (Next == 0)
                {
                    Next = HEAP_HEADER_LENGTH;
                }
            }
        }

        protected virtual void CheckTypeTable(Type type)
        {
            lock(SyncRoot)
            {
                if (type.Implements<ISerializer>() && type.Assembly.IsDynamic)
                {
                    type = type.BaseType;
                }

                if (!_codesByType.ContainsKey(type))
                {
                    var index = _typesByCode.Keys.DefaultIfEmpty().Max() + 1;
                    _codesByType.Add(type, index);
                    _typesByCode.Add(index, type);
                    TypesFile.Seek(0, SeekOrigin.End);
                    using (var sw = new StreamWriter(TypesFile, Encoding.UTF8, 4096, true))
                    {
                        sw.WriteLine(type.AssemblyQualifiedName);
                    }
                }
            }
        }

        protected virtual void CheckHeadRoom()
        {
            lock (SyncRoot)
            {
                if (MMVA.Capacity - Next < (int)((float)HEAP_HEAD_ROOM * 0.1f))
                {
                    if (BaseFile.Length - Next < (int)((float)HEAP_HEAD_ROOM * 0.1f))
                    {
                        if (AutoGrowSize > 0)
                        {
                            this.Grow(AutoGrowSize);
                        }
                        else
                        {
                            throw new OutOfMemoryException("There is no more room at the end of the heap to process this request.  Try compacting the heap to free up more room.");
                        }
                    }
                    else
                    {
                        ReleaseViewAccessor();
                        CreateView();
                    }
                }
            }
        }

        protected virtual void ReleaseViewAccessor()
        {
            try
            {
                Flush();
            }
            catch(Exception ex)
            {
                Logger.Log(ex, "An error occurred while releasing the persistent heap.");
            }

            if (MMVA != null)
            {
                MMVA.Release();
                MMVA.Flush();
                MMVA.Dispose();
                MMVA = null;
            }
            _filePtr = (byte*)IntPtr.Zero;
        }

        protected override void OnDisposeManagedResources()
        {
            _isInitialized = false;

            ReleaseViewAccessor();

            _addresses.Clear();
            _keys.Clear();

            lock(GlobalSyncRoot)
            {
                if (Interlocked.Decrement(ref COUNTER) == 0)
                {
                    TypesFile.Flush();
                    TypesFile.Dispose();
                    TypesFile = null;
                }
            }

            base.OnDisposeManagedResources();
        }
    }
}
