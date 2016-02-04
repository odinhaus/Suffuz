using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz
{
    public class AppDomainAwareDisposableManager : IManageDisposables
    {
        static List<IDisposable> _disposables = new List<IDisposable>();

        public AppDomainAwareDisposableManager()
        {
            AppDomain.CurrentDomain.DomainUnload += CurrentDomain_DomainUnload;
        }

        private void CurrentDomain_DomainUnload(object sender, EventArgs e)
        {
            this.Dispose();
        }

        public void Add(IDisposable disposable)
        {
            lock(_disposables)
            {
                if (!_disposables.Contains(disposable))
                {
                    _disposables.Add(disposable);
                }
            }
        }

        public void Remove(IDisposable disposable)
        {
            lock(_disposables)
            {
                _disposables.Remove(disposable);
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
            lock(_disposables)
            {
                foreach (var disposable in _disposables)
                {
                    disposable.Dispose();
                }
                _disposables.Clear();
                AppDomain.CurrentDomain.DomainUnload -= CurrentDomain_DomainUnload;
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
