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

namespace Altus.Suffūz.Collections
{
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

    public unsafe class PersistentHeap : PersistentCollectionBase, IPersistentHeap
    {
        protected int HEAD_ROOM;
        protected const int HEADER_LENGTH = 4 + 4 + 4 + 8;
        protected const int ITEM_ISVALID = 0;
        protected const int ITEM_INDEX = 1;
        protected const int ITEM_LENGTH = 9;
        protected const int ITEM_TYPE = 13;
        protected const int ITEM_DATA = 17;

        static int COUNTER = 0;
        static object GlobalSyncRoot = new object();

        bool _isInitialized = false;
        private byte* _filePtr;
        private BytePointerAdapter _ptr;
        private ulong _index = 0;
        Dictionary<ulong, int> _addresses = new Dictionary<ulong, int>();
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
        public PersistentHeap(string filePath, int maxSize = DEFAULT_HEAP_SIZE) : base(filePath, maxSize)
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

        protected Dictionary<ulong, int> Addresses {  get { return _addresses; } }

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
                System.IO.File.Delete(FilePath);
                First = Next = Last = 0;
                Initialize(FilePath, MaximumSize);
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
        /// Writes the item into the next available free location on the heap, and returns a key for that location which can be used for 
        /// subsequent Read, Free and OverwriteUnsafe calls.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public virtual ulong Add(object item)
        {
            var bytes = GetSerializer(item.GetType()).Serialize(item);

            lock (SyncRoot)
            {
                var itemType = item.GetType();
                CheckTypeTable(itemType);

                if (Next == HEADER_LENGTH)
                {
                    First = Last = Next;
                }
                else if (Next + ITEM_DATA + bytes.Length > File.Length)
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

                _index++;
                using (var scope = new FlushScope())
                {
                    scope.Enlist(this);
                    _ptr.Write(Next + ITEM_ISVALID, true); // record is valid
                    _ptr.Write(Next + ITEM_INDEX, _index); // index
                    _ptr.Write(Next + ITEM_LENGTH, bytes.Length); // length of bytes
                    _ptr.Write(Next + ITEM_TYPE, GetTypeCode(itemType)); // type index
                    _ptr.Write(Next + ITEM_DATA, bytes); // bytes
                    Last = Next;
                    Next += ITEM_DATA + bytes.Length;
                    UpdateHeaders();
                }
                
                CheckHeadRoom();
                _addresses.Add(_index, Last);
                return _index;
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
            var bytes = GetSerializer(item.GetType()).Serialize(item);
            var address = _addresses[key];

            lock (SyncRoot)
            {
                var itemType = item.GetType();
                CheckTypeTable(itemType);

                using (var scope = new FlushScope())
                {
                    scope.Enlist(this);
                    _ptr.Write(address + ITEM_ISVALID, true); // record is valid
                    _ptr.Write(address + ITEM_INDEX, key); // index
                    _ptr.Write(address + ITEM_LENGTH, bytes.Length); // length of bytes
                    _ptr.Write(address + ITEM_TYPE, GetTypeCode(itemType)); // type index
                    _ptr.Write(address + ITEM_DATA, bytes); // bytes
                }
                return key;
            }
        }

        public virtual ulong Write(object item, ulong key)
        {
            var bytes = GetSerializer(item.GetType()).Serialize(item);
            var address = _addresses[key];

            lock (SyncRoot)
            {
                var itemType = item.GetType();
                CheckTypeTable(itemType);

                var length = _ptr.ReadInt32(address + ITEM_LENGTH);
                var typeCode = GetTypeCode(itemType);
                using (var scope = new FlushScope())
                {
                    if (length == bytes.Length
                        && typeCode == _ptr.ReadInt32(address + ITEM_TYPE))
                    {
                        scope.Enlist(this);
                        _ptr.Write(address + ITEM_ISVALID, true); // record is valid
                        _ptr.Write(address + ITEM_INDEX, key); // index
                        _ptr.Write(address + ITEM_LENGTH, bytes.Length); // length of bytes
                        _ptr.Write(address + ITEM_TYPE, typeCode); // type index
                        _ptr.Write(address + ITEM_DATA, bytes); // bytes
                    }
                    else
                    {
                        Free(key);
                        key = Add(item);
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
            byte[] bytes;
            Type itemType;
            lock (SyncRoot)
            {
                var address = _addresses[key];
                var isValid = _ptr.ReadBoolean(address + ITEM_ISVALID);
                if (isValid && _ptr.ReadUInt64(address + ITEM_INDEX) == key)
                {
                    var len = _ptr.ReadInt32(address + ITEM_LENGTH);
                    bytes = _ptr.ReadBytes(address + ITEM_DATA, len);
                    itemType = GetCodeType(_ptr.ReadInt32(address + ITEM_TYPE));
                }
                else return null;
            }
            return GetSerializer(itemType).Deserialize(bytes, itemType);
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
                    _ptr.Write(address, false);
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
                    var address = HEADER_LENGTH;
                    var delta = 0;
                    var block = 0;
                    First = Last = 0;
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
                                        _ptr.Write(address + i, _ptr.ReadByte(address + delta + i));
                                    }
                                    // invalidate delta block
                                    _ptr.Write(address + block + ITEM_ISVALID, false);
                                    _ptr.Write(address + block + ITEM_INDEX, (ulong)0);
                                    _ptr.Write(address + block + ITEM_TYPE, 0);
                                    _ptr.Write(address + block + ITEM_LENGTH, delta - ITEM_DATA);
                                    // repeat, now with valid data at address
                                }
                            }
                        }
                    }
                    // wipe free space at end of file
                    SparseFile.SetZero(File, Next, MMVA.Capacity - Next);
                    // update new index values
                    LoadIndices();
                    UpdateHeaders();
                }
                else
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
            if (MMVA != null)
            {
                MMVA.Flush();
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
                HEAD_ROOM = (int)((float)maxSize * 0.2f);
                lock(GlobalSyncRoot)
                {
                    if (TypesFile == null)
                    {
                        TypesFile = new FileStream("Type_List.bin", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                        LoadTypes();
                    }
                }

                ReadHeaders();
                if (Next == 0)
                {
                    Next = HEADER_LENGTH;
                }

                CreateView();
                LoadIndices();
                UpdateHeaders();
                _isInitialized = true;
            }
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
            var size = Math.Min(Next + HEAD_ROOM + HEADER_LENGTH, File.Length);

            MMVA = MMF.CreateViewAccessor(0, size);
            _filePtr = MMVA.Pointer(0);
            _ptr = new BytePointerAdapter(ref _filePtr, 0, size);
        }

        protected virtual void LoadIndices()
        {
            var address = HEADER_LENGTH;
            ulong key;
            _addresses.Clear();
            while(address < Next)
            {
                key = _ptr.ReadUInt64(address + 1);
                if (_ptr.ReadBoolean(address))
                {
                    if (First == 0)
                    {
                        First = address;
                    }
                    Last = address;
                    _addresses.Add(key, address);
                }
                address += ITEM_DATA + _ptr.ReadInt32(address + ITEM_LENGTH);
            }
        }

        protected virtual int GetNext(int address, bool isValid)
        {
            lock(SyncRoot)
            {
                var currentValid = _ptr.ReadBoolean(address);
                while(currentValid != isValid && address < Next)
                {
                    var len = _ptr.ReadInt32(address + ITEM_LENGTH);
                    address += ITEM_DATA + len;
                    currentValid = _ptr.ReadBoolean(address);
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
                using (var scope = new FlushScope())
                {
                    scope.Enlist(this);
                    _ptr.Write(0, First);
                    _ptr.Write(4, Last);
                    _ptr.Write(8, Next);
                    _ptr.Write(12, _index);
                }
            }
        }

        protected virtual void ReadHeaders()
        {
            lock (SyncRoot)
            {
                File.Seek(0, SeekOrigin.Begin);
                First = File.ReadInt32();
                Last = File.ReadInt32();
                Next = File.ReadInt32();
                _index = File.ReadUInt64();
                File.Seek(0, SeekOrigin.Begin);
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
            if (MMVA.Capacity - Next < (int)((float)HEAD_ROOM * 0.1f))
            {
                lock (SyncRoot)
                {
                    ReleaseViewAccessor();
                    CreateView();
                }
            }
        }

        protected virtual void ReleaseViewAccessor()
        {
            if (MMVA != null)
            {
                MMVA.Release();
                MMVA.Flush();
                MMVA.Dispose();
                MMVA = null;
            }
            _ptr = null;
            _filePtr = (byte*)IntPtr.Zero;
        }

        protected override void OnDisposeManagedResources()
        {
            _isInitialized = false;

            ReleaseViewAccessor();

            _addresses.Clear();

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
