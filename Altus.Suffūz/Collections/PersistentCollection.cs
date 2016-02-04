using Altus.Suffūz.Collections.IO;
using Altus.Suffūz.Serialization;
using Altus.Suffūz.Serialization.Binary;
using Altus.Suffūz.Threading;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace Altus.Suffūz.Collections
{
    public abstract class PersistentCollection : ICollection, IEnumerable, IDisposable, IFlush, IPersistentCollection
    {
        static System.Collections.Generic.Dictionary<Type, ISerializer> _serializers = new System.Collections.Generic.Dictionary<Type, ISerializer>();
        static Dictionary<string, PersistentCollection> _shares = new Dictionary<string, PersistentCollection>();
        static readonly string DEFAULT_DATA_ROOT = "";
        /// <summary>
        /// Default heap size in bytes (10 Mb)
        /// </summary>
        public const int DEFAULT_HEAP_SIZE = 1024 * 1024 * 10;

        static PersistentCollection()
        {
            try
            {
                DEFAULT_DATA_ROOT = ConfigurationManager.AppSettings["collectionsDataPath"];
            }
            catch { }

            if (DEFAULT_DATA_ROOT == null)
            {
                DEFAULT_DATA_ROOT = "";
            }

            if (!string.IsNullOrEmpty(DEFAULT_DATA_ROOT))
            {
                var di = Directory.CreateDirectory(DEFAULT_DATA_ROOT);
                if (!Path.IsPathRooted(DEFAULT_DATA_ROOT))
                {
                    DEFAULT_DATA_ROOT = di.FullName;
                }
            }
        }

        protected PersistentCollection() : this(Path.GetTempFileName())
        {
        }

        protected PersistentCollection(string filePath, int maxSize = DEFAULT_HEAP_SIZE, bool isTransactional = false, ExclusiveLock syncLock = null)
        {
            if (syncLock == null)
            {
                syncLock = new ExclusiveLock(filePath);
            }
            SyncLock = syncLock;
            IsTransactional = isTransactional;
            First = Next = Last = 0;
            Initialize(filePath, maxSize);

            App.Resolve<IManageDisposables>().Add(this);
        }

        protected PersistentCollection(PersistentCollection collection)
        {
            SyncLock = collection.SyncLock;
            Initialize(collection);
            App.Resolve<IManageDisposables>().Add(this);
        }

        protected void Initialize(PersistentCollection collection)
        {
            MaximumSize = collection.MaximumSize;
            BaseFilePath = collection.BaseFilePath;
            BaseFile = collection.BaseFile;
            BaseMMF = collection.BaseMMF;
            Initialize(false, BaseFilePath, collection.MaximumSize);
        }

        protected void Initialize(string filePath, int maxSize)
        {
            filePath = FilePath(filePath);

            SyncLock.Lock(() =>
            {
                MaximumSize = maxSize;
                BaseFilePath = filePath;
                BaseFile = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                var isNew = BaseFile.Length == 0;

                if (BaseFile.Length == 0)
                {
                    BaseFile.SetLength(maxSize);
                    SparseFile.MakeSparse(BaseFile);
                    SparseFile.SetZero(BaseFile, 0, BaseFile.Length);
                }
                else if (maxSize != BaseFile.Length)
                {
                    BaseFile.SetLength(maxSize);
                    SparseFile.SetZero(BaseFile, BaseFile.Length, maxSize);
                }

                BaseMMF = MemoryMappedFile.CreateFromFile(
                       BaseFile,
                       MappedName(),
                       maxSize,
                       MemoryMappedFileAccess.ReadWrite,
                       null,
                       HandleInheritability.None,
                       false);

                Initialize(isNew, filePath, maxSize);
            });
        }

        protected string FilePath(string path)
        {
            if (!Path.IsPathRooted(path))
            {
                return Path.Combine(DEFAULT_DATA_ROOT, path);
            }
            else
            {
                return path;
            }
        }

        protected string MappedName()
        {
            return string.Format("{0}:{1}_MMF", App.InstanceName, Path.GetFileNameWithoutExtension(BaseFile.Name));
        }

        protected abstract void Initialize(bool isNewFile, string filePath, int maxSize);
        public abstract void Clear();
        public abstract void Clear(bool compact);
        public abstract void Compact();
        public abstract void WriteUnsafe(int address, byte[] data);

        protected virtual ISerializer GetSerializer(Type itemType)
        {
            ISerializer s;
            lock(_serializers)
            {
                if (!_serializers.TryGetValue(itemType, out s))
                {
                    var serializationCtx = App.Resolve<ISerializationContext>();
                    if (serializationCtx == null)
                    {
                        s = new ILSerializerBuilder().CreateSerializerType(itemType);
                    }
                    else
                    {
                        s = serializationCtx.GetSerializer(itemType, StandardFormats.BINARY);
                    }
                    _serializers.Add(itemType, s);
                }
            }
            return s;
        }

        public bool IsTransactional { get; private set; }
        public int MaximumSize { get; private set; }
        public string BaseFilePath { get; private set; }
        protected FileStream BaseFile { get; private set; }
        protected MemoryMappedFile BaseMMF { get; private set; }
        public object SyncRoot { get { return SyncLock; } }
        public virtual int Length { get { return Next; } }
        /// <summary>
        /// Setting this to a non-zero value allows the collection to automatically grow in size when an OutOfMemory condition occurs.
        /// </summary>
        public int AutoGrowSize { get; set; }
        /// <summary>
        /// Setting this value to true will cause the collection to be compacted first before it is Grown.
        /// </summary>
        public bool CompactBeforeGrow { get; set; }

        public abstract int Count { get; }

        protected int First { get; set; }
        protected int Next { get; set; }
        protected int Last { get; set; }

        public bool IsSynchronized
        {
            get
            {
                return true;
            }
        }

        public void CopyTo(Array array, int index)
        {
            var en = GetEnumerator();
            var i = 0;
            while(en.MoveNext())
            {
                array.SetValue(en.Current, index + i);
                i++;
            }
        }

        public virtual void Grow(int capacityToAdd)
        {
            SyncLock.Lock(() =>
            {
                lock (SyncRoot)
                {
                    if (CompactBeforeGrow)
                    {
                        var currentSize = Next;
                        Compact();
                        var delta = currentSize - Next;
                        capacityToAdd -= delta;
                    }

                    if (capacityToAdd > 0)
                    {
                        OnDisposeManagedResources();
                        Initialize(BaseFilePath, MaximumSize + capacityToAdd);
                    }
                }
            });
        }

        public abstract void Flush();
        public abstract IEnumerator GetEnumerator();

        public bool IsDisposed { get { return disposed; } }

        public ExclusiveLock SyncLock { get; private set; }

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
        /// Dispose managed resources.  Overridden implementations MUST call base.OnDisposeManagedResources() to prevent 
        /// handle locking and memory leaks.
        /// </summary>
        protected virtual void OnDisposeManagedResources()
        {
            Flush();

            if (this.BaseMMF != null)
            {
                this.BaseMMF.Dispose();
                this.BaseMMF = null;
            }

            if (this.BaseFile != null)
            {
                this.BaseFile.Close();
                this.BaseFile.Dispose();
                this.BaseFile = null;
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
