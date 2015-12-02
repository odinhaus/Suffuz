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

namespace Altus.Suffūz.Collections
{
    public class Heap<TValue> : Heap, IEnumerable<TValue>
    {
        public Heap() : base()
        {
        }

        public Heap(string filePath, int maxSize = 1024 * 1024 * 1024) : base(filePath, maxSize)
        {

        }

        protected override void CheckTypeTable(Type type)
        {
            if (type != typeof(TValue))
                throw new InvalidCastException(string.Format("The type {0} is not supported by this Heap.", type.Name));
        }

        public virtual ulong Write(TValue item)
        {
            return base.Write(item);
        }

        public virtual ulong OverwriteUnsafe(TValue item, ulong key)
        {
            return base.OverwriteUnsafe(item, key);
        }

        public virtual new TValue Read(ulong key)
        {
            return base.Read<TValue>(key);
        }

        protected override void LoadTypes()
        {
            // do nothing - only one type
        }

        protected override Type GetCodeType(int code)
        {
            return typeof(TValue);
        }

        protected override int GetTypeCode(Type type)
        {
            return 1;
        }

        public new IEnumerator<TValue> GetEnumerator()
        {
            var en = base.GetEnumerator();
            while(en.MoveNext())
            {
                yield return (TValue)en.Current;
            }
        }
    }

    public unsafe class Heap : CollectionBase
    {
        int HEAD_ROOM;
        const int HEADER_LENGTH = 4 + 4 + 4 + 8;
        const int ITEM_ISVALID = 0;
        const int ITEM_INDEX = 1;
        const int ITEM_LENGTH = 9;
        const int ITEM_TYPE = 13;
        const int ITEM_DATA = 17;


        bool _isInitialized = false;
        private byte* _filePtr;
        private BytePointerAdapter _ptr;
        private ulong _index = 0;
        System.Collections.Generic.Dictionary<ulong, int> _addresses = new System.Collections.Generic.Dictionary<ulong, int>();
        System.Collections.Generic.Dictionary<int, Type> _typesByCode = new System.Collections.Generic.Dictionary<int, Type>();
        System.Collections.Generic.Dictionary<Type, int> _codesByType = new System.Collections.Generic.Dictionary<Type, int>();

        /// <summary>
        /// Create a new heap using a system generated file location, and DEFAULT_HEAP_SIZE (10Mb)
        /// </summary>
        public Heap() : base()
        {
        }
        /// <summary>
        /// Create a new heap using a supplied file path and optional maximum capacity
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="maxSize"></param>
        public Heap(string filePath, int maxSize = DEFAULT_HEAP_SIZE) : base(filePath, maxSize)
        {
            
        }

        /// <summary>
        /// Returns the current commited length of the heap in bytes
        /// </summary>
        public int Length
        {
            get
            {
                return Next;
            }
        }

        protected MemoryMappedViewAccessor MMVA { get; private set; }
        protected FileStream TypesFile { get; private set; }

        /// <summary>
        /// Writes the item into the next available free location on the heap, and returns a key for that location which can be used for 
        /// subsequent Read, Free and OverwriteUnsafe calls.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public virtual ulong Write(object item)
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
                    throw new OutOfMemoryException("There is no more room at the end of the heap to process this request.  Try compacting the heap to free up more room.");
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
        public virtual ulong OverwriteUnsafe(object item, ulong key)
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
                    _ptr.Write(address + ITEM_INDEX, _index); // index
                    _ptr.Write(address + ITEM_LENGTH, bytes.Length); // length of bytes
                    _ptr.Write(address + ITEM_TYPE, GetTypeCode(itemType)); // type index
                    _ptr.Write(address + ITEM_DATA, bytes); // bytes
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
                _ptr.Write(address, false);
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
        public virtual void Compact()
        {
            lock(SyncRoot)
            {
                var end = MMVA.Capacity;
                var address = HEADER_LENGTH;
                var delta = 0;
                var block = 0;
                First = Last = 0;
                while(address != -1)
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
                // wipe free space at end of file
                SparseFile.SetZero(File, Next, MMVA.Capacity - Next);
                // update new index values
                LoadIndices();
                UpdateHeaders();
            }
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
                TypesFile = new FileStream(Path.ChangeExtension(filePath, "types"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);

                ReadHeaders();
                if (Next == 0)
                {
                    Next = HEADER_LENGTH;
                }

                CreateView();
                LoadIndices();
                LoadTypes();
                UpdateHeaders();
                _isInitialized = true;
            }
        }

        protected virtual void LoadTypes()
        {
            lock(SyncRoot)
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
        }

        protected override void OnDisposeManagedResources()
        {
            ReleaseViewAccessor();

            if (TypesFile != null)
            {
                TypesFile.Flush();
                TypesFile.Close();
                TypesFile.Dispose();
                TypesFile = null;
            }
        }
    }
}
