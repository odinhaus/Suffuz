using Altus.Suffūz.Collections.IO;
using Altus.Suffūz.Serialization;
using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Collections
{
    public abstract class CollectionBase : IEnumerable, IDisposable, IFlush
    {
        static System.Collections.Generic.Dictionary<Type, ISerializer> _serializers = new System.Collections.Generic.Dictionary<Type, ISerializer>();
        /// <summary>
        /// Default heap size in bytes (10 Mb)
        /// </summary>
        public const int DEFAULT_HEAP_SIZE = 1024 * 1024 * 10;

        protected CollectionBase() : this(Path.GetTempFileName())
        {
        }

        protected CollectionBase(string filePath, int maxSize = DEFAULT_HEAP_SIZE)
        {
            SyncRoot = new object();
            File = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            var isNew = File.Length == 0;
            First = Next = Last = 0;

            if (File.Length == 0)
            {
                File.SetLength(maxSize);
                SparseFile.MakeSparse(File);
                SparseFile.SetZero(File, 0, File.Length);
            }
            else if (maxSize != File.Length)
            {
                File.SetLength(maxSize);
                SparseFile.SetZero(File, File.Length, maxSize);
            }

            MMF = MemoryMappedFile.CreateFromFile(
                   File,
                   Path.GetFileNameWithoutExtension(File.Name) + "_MMF",
                   maxSize,
                   MemoryMappedFileAccess.ReadWrite,
                   null,
                   HandleInheritability.None,
                   false);

            Initialize(isNew, filePath, maxSize);
        }

        protected abstract void Initialize(bool isNewFile, string filePath, int maxSize);


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


        public int MaximumSize { get; private set; }
        public string FilePath { get; private set; }
        protected FileStream File { get; private set; }
        protected MemoryMappedFile MMF { get; private set; }
        public object SyncRoot { get; private set; }

        public int First { get; protected set; }
        public int Next { get; protected set; }
        public int Last { get; protected set; }

        public abstract void Flush();
        public abstract IEnumerator GetEnumerator();

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
                    Flush();

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
        /// Dispose managed resources
        /// </summary>
        protected virtual void OnDisposeManagedResources()
        {
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
