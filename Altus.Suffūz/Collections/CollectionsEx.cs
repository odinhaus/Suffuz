using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Collections.Linq
{
    public static class CollectionsEx
    {
        public static object First(this ICollection collection)
        {
            var en = collection.GetEnumerator();
            en.MoveNext();
            return en.Current;
        }
    }
}
