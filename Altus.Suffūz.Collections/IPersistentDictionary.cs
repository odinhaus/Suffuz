using System.Collections.Generic;

namespace Altus.Suffūz.Collections
{
    public interface IPersistentDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IPersistentCollection
    {
        bool AllowOverwrites { get; }
    }
}