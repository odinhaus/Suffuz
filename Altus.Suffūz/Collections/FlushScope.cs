using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Collections
{
    public class FlushScope : IDisposable
    {
        [ThreadStatic]
        static Stack<FlushScope> _scopes = new Stack<FlushScope>();
        [ThreadStatic]
        static System.Collections.Generic.List<IFlush> _flushables = new System.Collections.Generic.List<IFlush>();

        public FlushScope()
        {
            if (_scopes == null)
                _scopes = new Stack<FlushScope>();

            _scopes.Push(this);
        }

        public void Enlist(IFlush flushable)
        {
            if (_flushables == null)
                _flushables = new System.Collections.Generic.List<IFlush>();

            if (!_flushables.Contains(flushable))
            {
                _flushables.Add(flushable);
            }
        }

        public static FlushScope Current
        {
            get
            {
                if (_scopes.Count > 0)
                    return _scopes.Peek();
                else
                    return null;
            }
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
                    _scopes.Pop();

                    if (_scopes.Count == 0 && _flushables != null)
                    {
                        foreach(var flushable in _flushables)
                        {
                            flushable.Flush();
                        }
                        _flushables.Clear();
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
