using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Collections
{
    public interface IPersistentList : IPersistentCollection, IList
    {
    }

    public interface IPersistentList<TValue> : IPersistentList, IList<TValue>
    {
    }
}
