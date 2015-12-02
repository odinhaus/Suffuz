using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altus.Suffūz.Collections.IO;
using Altus.Suffūz.Serialization;
using Altus.Suffūz.Serialization.Binary;

namespace Altus.Suffūz.Collections
{
    public class PersistentList<TValue> : IList<TValue>, IDisposable
    {
        private System.Collections.Generic.List<Page> _pages = new System.Collections.Generic.List<Page>();

        public PersistentList() : this(Path.GetTempFileName())
        {

        }

        public PersistentList(string filePath, int maxFileSize = 1024 * 1024 * 1024)
        {
            var serializationCtx = App.Resolve<ISerializationContext>();
            if (serializationCtx == null)
            {
                Serializer = new ILSerializerBuilder().CreateSerializerType(typeof(TValue));
            }
            else
            {
                Serializer = serializationCtx.GetSerializer(typeof(TValue), StandardFormats.BINARY);
            }

            File = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            var isNew = File.Length == 0;

            if (File.Length == 0)
            {
                File.SetLength(maxFileSize);
                SparseFile.MakeSparse(File);
                SparseFile.SetZero(File, 0, File.Length);
            }
            

            MMF = MemoryMappedFile.CreateFromFile(
                   File,
                   Path.GetFileNameWithoutExtension(File.Name) + "_MMF",
                   maxFileSize,
                   MemoryMappedFileAccess.ReadWrite,
                   null,
                   HandleInheritability.None,
                   false);

            if (isNew)
            {
                InitializeNewFile();
            }
            else
            {
                InitializeExistingFile();
            }
        }

        private void InitializeNewFile()
        {
            _pages.Add(new Page(this, MMF, 0));
            File.Seek(0, SeekOrigin.Begin);
            File.Write(1);
        }

        private void InitializeExistingFile()
        {
            File.Seek(0, SeekOrigin.Begin);
            var pages = File.ReadInt32();
            if (pages == 0)
                InitializeNewFile();
            else
            {
                for (int i = 0; i < pages; i++)
                {
                    _pages.Add(new Page(this, MMF, i * Page.PAGE_SIZE));
                }
            }
        }

        public FileStream File { get; private set; }
        public MemoryMappedFile MMF { get; private set; }
        public ISerializer Serializer { get; set; }

        public TValue this[int index]
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public int Count
        {
            get
            {
                return _pages.Sum(p => p.Count);
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public void Add(TValue item)
        {
            var page = _pages.Last();
            page.Add(item);
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(TValue item)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(TValue[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            foreach(var page in _pages)
            {
                foreach(var item in page)
                {
                    yield return item;
                }
            }
        }

        public int IndexOf(TValue item)
        {
            throw new NotImplementedException();
        }

        public void Insert(int index, TValue item)
        {
            throw new NotImplementedException();
        }

        public bool Remove(TValue item)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private unsafe class Page : IList<TValue>, IDisposable
        {
            public const int PAGE_COUNT_HEADER = 4;
            public const int ITEM_COUNT_HEADER = 4;
            public const int FIRST_ITEM_HEADER = 4;
            public const int LAST_ITEM_HEADER = 4;
            public const int PAGE_SIZE = 64 * 1024 * 2; // 128kb
            object _sync = new object();
            bool _initialized = false;
            byte* _filePtr;
            BytePointerAdapter _ptr;

            public Page(PersistentList<TValue> list, MemoryMappedFile mmf, int offset)
            {
                List = list;
                Offset = offset + PAGE_COUNT_HEADER;
                MMF = mmf;
                First = -1;
                Next = -1;
                Last = -1;
               
                MMVA = mmf.CreateViewAccessor(Offset, PAGE_SIZE);
                _filePtr = MMVA.Pointer(Offset);
                _ptr = new BytePointerAdapter(ref _filePtr, Offset, Offset + PAGE_SIZE);

                _initialized = _ptr.ReadInt32(Offset) == 0;

                if (_initialized)
                {
                    Count = 0;
                    Next = Offset + ITEM_COUNT_HEADER + FIRST_ITEM_HEADER + LAST_ITEM_HEADER;
                    UpdateHeaders();
                    MMVA.Flush();
                }
                else
                {
                    Initialize();
                }
            }

            public PersistentList<TValue> List { get; private set; }
            public int Offset { get; private set; }
            public MemoryMappedFile MMF { get; private set; }
            public MemoryMappedViewAccessor MMVA { get; private set; }
            public int First { get; private set; }
            public int Next { get; private set; }
            public int Last { get; private set; }

            int _count = 0;
            public int Count {  get; private set; }

            public bool IsReadOnly
            {
                get
                {
                    return false;
                }
            }

            public TValue this[int index]
            {
                get
                {
                    throw new NotImplementedException();
                }

                set
                {
                    throw new NotImplementedException();
                }
            }

            public void Add(TValue item)
            {
                var bytes = List.Serializer.Serialize(item);

                // Previous Item
                // Length
                // Data
                lock(_sync)
                {
                    _ptr.Write(Offset + Next, Last); // write 4 bytes
                    _ptr.Write(Offset + Next + 4, bytes.Length); // write 4 bytes
                    _ptr.Write(Offset + Next + 4 + 4, bytes); // write data

                    Last = Next;
                    Next += 4 + 4 + bytes.Length;
                    if (First == -1)
                    {
                        First = Last;
                    }
                    Count++;
                    UpdateHeaders();
                }
            }

            public bool Remove(TValue item)
            {
                throw new NotImplementedException();
            }

            public void Insert(int index, TValue item)
            {
                throw new NotImplementedException();
            }

            private void UpdateHeaders()
            {
                _ptr.Write(Offset, Count); // count
                _ptr.Write(Offset + ITEM_COUNT_HEADER, First); // first item address
                _ptr.Write(Offset + ITEM_COUNT_HEADER + FIRST_ITEM_HEADER, Last); // last item address
            }

            private void Initialize()
            {
                if (!_initialized)
                {
                    Count = _ptr.ReadInt32(Offset);
                    First = _ptr.ReadInt32(Offset + ITEM_COUNT_HEADER);
                    Last = _ptr.ReadInt32(Offset + ITEM_COUNT_HEADER + FIRST_ITEM_HEADER);

                    if (Count == 0)
                    {
                        Next = Offset + ITEM_COUNT_HEADER + FIRST_ITEM_HEADER + LAST_ITEM_HEADER;
                    }
                    else
                    {
                        Next = Last + _ptr.ReadInt32(Offset + Last + 4) + 4 + 4;
                    }

                    _initialized = true;
                }
            }

            public IEnumerator<TValue> GetEnumerator()
            {
                bool read = Count > 0;
                var addr = First;
                while(read)
                {
                    var length = _ptr.ReadInt32(Offset + 4 + addr);
                    var bytes = _ptr.ReadBytes(Offset + 4 + 4 + addr, length);
                    yield return (TValue)List.Serializer.Deserialize(bytes, typeof(TValue));
                }
                yield break;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public int IndexOf(TValue item)
            {
                throw new NotImplementedException();
            }

            public void RemoveAt(int index)
            {
                throw new NotImplementedException();
            }

            public void Clear()
            {
                throw new NotImplementedException();
            }

            public bool Contains(TValue item)
            {
                throw new NotImplementedException();
            }

            public void CopyTo(TValue[] array, int arrayIndex)
            {
                throw new NotImplementedException();
            }
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
                        // Dispose managed resources.
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
            /// Dispose managed resources
            /// </summary>
            protected virtual void OnDisposeManagedResources()
            {
                if (_filePtr != (byte*)IntPtr.Zero && this.MMVA != null)
                {
                    this.MMVA.Release();
                }

                if(this.MMVA != null)
                {
                    this.MMVA.Flush();
                    this.MMVA.Dispose();
                    this.MMVA = null;
                }
                this.MMF = null;
            }

            /// <summary>
            /// Dispose unmanaged (native resources)
            /// </summary>
            protected virtual void OnDisposeUnmanagedResources()
            {
            }

            #endregion

        }

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
                    // Dispose managed resources.
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
        /// Dispose managed resources
        /// </summary>
        protected virtual void OnDisposeManagedResources()
        {
            foreach(var page in _pages)
            {
                page.Dispose();
            }

            if (this.MMF != null)
            {
                this.MMF.Dispose();
                this.MMF = null;
            }

            if (this.File != null)
            {
                this.File.Close();
                this.File.Dispose();
                this.File = null;
            }
        }

        /// <summary>
        /// Dispose unmanaged (native resources)
        /// </summary>
        protected virtual void OnDisposeUnmanagedResources()
        {
        }

        #endregion
    }
}
