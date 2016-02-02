using Altus.Suffūz.Scheduling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Collections
{
    public class PersistentCollectionManager : IManagePersistentCollections
    {
        public const string GLOBAL_HEAP = "global_heap.bin";
        public const int GLOBAL_HEAP_COMPACT_INTERVAL = 30000;

        static Dictionary<string, IPersistentCollection> _collections = new Dictionary<string, IPersistentCollection>();

        static PersistentCollectionManager()
        {
            AppDomain.CurrentDomain.DomainUnload += CurrentDomain_DomainUnload;
        }

        private static void CurrentDomain_DomainUnload(object sender, EventArgs e)
        {
            DisposeAll();
        }

        public IPersistentHeap GlobalHeap
        {
            get
            {
                return GetOrCreate<PersistentHeap>(GLOBAL_HEAP, (name) =>
                {
                    var heap = new PersistentHeap(name)
                    {
                        AutoGrowSize = 1024 * 1024 * 10
                    };
                    App.Resolve<IScheduler>().Schedule(GLOBAL_HEAP_COMPACT_INTERVAL, () => heap.Compact());
                    return heap;
                });
            }
        }

        /// <summary>
        /// Gets and existing entry or creates a new one, if none exists.
        /// </summary>
        /// <typeparam name="TCollection"></typeparam>
        /// <param name="fileName"></param>
        /// <param name="creator"></param>
        /// <returns></returns>
        public TCollection GetOrCreate<TCollection>(string fileName, Func<string, TCollection> creator) where TCollection : IPersistentCollection
        {
            IPersistentCollection collection;
            lock(_collections)
            {
                if (!_collections.TryGetValue(fileName, out collection) || collection.IsDisposed)
                {
                    collection = creator(fileName);
                    _collections[fileName] = collection;
                }
            }
            return (TCollection)collection;
        }

        /// <summary>
        /// Registers an existing collection with the manager, optionally overwriting and disposing any existing entries with the 
        /// same file name.
        /// </summary>
        /// <typeparam name="TCollection"></typeparam>
        /// <param name="fileName"></param>
        /// <param name="collection"></param>
        /// <param name="allowOverwrite"></param>
        public void Register<TCollection>(string fileName, TCollection collection, bool allowOverwrite = false) where TCollection : IPersistentCollection
        {
            lock(_collections)
            {
                if (_collections.ContainsKey(fileName))
                {
                    if (allowOverwrite)
                    {
                        var existing = _collections[fileName];
                        existing.Dispose();
                    }
                    else throw new InvalidOperationException("A collection with the same path already exists.");
                }

                _collections[fileName] = collection;
            }
        }

        /// <summary>
        /// Calls dispose on each managed collection, and clears the collection cache.  This method is automatically invoked 
        /// when the current AppDomain is unloaded, to prevent leaked file handles.
        /// </summary>
        public static void DisposeAll()
        {
            lock(_collections)
            {
                foreach(var item in _collections.Values)
                {
                    item.Dispose();
                }
                _collections.Clear();
            }
        }
    }
}
