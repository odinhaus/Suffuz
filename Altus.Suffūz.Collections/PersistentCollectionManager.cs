using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Collections
{
    public class PersistentCollectionManager : IManagePersistentCollections
    {
        static Dictionary<string, IPersistentCollection> _collections = new Dictionary<string, IPersistentCollection>();
        public TCollection GetOrCreate<TCollection>(string fileName, Func<string, TCollection> creator) where TCollection : IPersistentCollection
        {
            IPersistentCollection collection;
            lock(_collections)
            {
                if (!_collections.TryGetValue(fileName, out collection))
                {
                    collection = creator(fileName);
                }
            }
            return (TCollection)collection;
        }
    }
}
