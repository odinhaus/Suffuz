using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Collections
{
    public interface IManagePersistentCollections
    {
        TCollection GetOrCreate<TCollection>(string fileName, Func<string, TCollection> creator) 
            where TCollection : IPersistentCollection;

        IPersistentHeap GlobalHeap { get; }
    }
}
