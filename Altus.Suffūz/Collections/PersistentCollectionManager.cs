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
    }
}
