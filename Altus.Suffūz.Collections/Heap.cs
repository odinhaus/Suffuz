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
    public unsafe class Heap : CollectionBase
    {
        int HEAD_ROOM;
        const int HEADER_LENGTH = 4 + 4 + 4 + 8;
        const int ITEM_ISVALID = 0;
        const int ITEM_INDEX = 1;
        const int ITEM_LENGTH = 9;
        const int ITEM_TYPE = 13;
        const int ITEM_DATA = 17;

        private byte* _filePtr;
        private BytePointerAdapter _ptr;
        private ulong _index = 0;

        public Heap() : base()
        {
        }

        public Heap(string filePath, int maxSize = 1024 * 1024 * 1024) : base(filePath, maxSize)
        {
            
        }

        public MemoryMappedViewAccessor MMVA { get; private set; }
        public bool IsDirty { get; private set; }
        public FileStream TypesFile { get; private set; }

        System.Collections.Generic.Dictionary<ulong, int> _addresses = new System.Collections.Generic.Dictionary<ulong, int>();
        System.Collections.Generic.Dictionary<int, Type> _typesByCode = new System.Collections.Generic.Dictionary<int, Type>();
        System.Collections.Generic.Dictionary<Type, int> _codesByType = new System.Collections.Generic.Dictionary<Type, int>();
        public ulong Write(object item)
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
                    _ptr.Write(Next + ITEM_TYPE, _codesByType[itemType]); // type index
                    _ptr.Write(Next + ITEM_DATA, bytes); // bytes
                    Last = Next;
                    Next += ITEM_DATA + bytes.Length;
                    UpdateHeaders();
                }
                
                CheckHeadRoom();
                IsDirty = true;
                _addresses.Add(_index, Last);
                return _index;
            }
        }

        public object Read(ulong key)
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
                    itemType = _typesByCode[_ptr.ReadInt32(address + ITEM_TYPE)];
                }
                else return null;
            }
            return GetSerializer(itemType).Deserialize(bytes, itemType);
        }

        public TValue Read<TValue>(ulong key)
        {
            return (TValue)Read(key);
        }

        public void InValidate(ulong key)
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

        public void Compact()
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

        public override void Flush()
        {
            if (MMVA != null)
            {
                MMVA.Flush();
            }
        }

        bool _isInitialized = false;
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

        protected void LoadTypes()
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

        protected void CreateView()
        {
            var size = Math.Min(Next + HEAD_ROOM + HEADER_LENGTH, File.Length);

            MMVA = MMF.CreateViewAccessor(0, size);
            _filePtr = MMVA.Pointer(0);
            _ptr = new BytePointerAdapter(ref _filePtr, 0, size);
        }

        protected void LoadIndices()
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

        protected int GetNext(int address, bool isValid)
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

        protected void UpdateHeaders()
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

        protected void ReadHeaders()
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

        protected void CheckTypeTable(Type type)
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

        protected void CheckHeadRoom()
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

        protected void ReleaseViewAccessor()
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
