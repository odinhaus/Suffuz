using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altus.Suffūz.Collections.IO;
using Altus.Suffūz.Serialization;
using Altus.Suffūz.Serialization.Binary;

namespace Altus.Suffūz.Collections
{
    public class PersistentList<TValue> : PersistentList, IPersistentList<TValue>
    {
        public PersistentList() : base()
        {
        }

        public PersistentList(string filePath, int maxSize = 1024 * 1024 * 1024) : base(filePath, maxSize)
        {

        }

        TValue IList<TValue>.this[int index]
        {
            get
            {
                return (TValue)base[index];
            }

            set
            {
                base[index] = value;
            }
        }

        public void Add(TValue item)
        {
            base.Add(item);
        }

        public bool Contains(TValue item)
        {
            return base.Contains(item);
        }

        public void CopyTo(TValue[] array, int arrayIndex)
        {
            var i = 0;
            foreach(var item in this)
            {
                array[arrayIndex + i] = item;
                i++;
            }
        }

        public int IndexOf(TValue item)
        {
            return base.IndexOf(item);
        }

        public void Insert(int index, TValue item)
        {
            base.Insert(index, item);
        }

        public bool Remove(TValue item)
        {
            var index = IndexOf(item);
            if (index >= 0)
            {
                var key = Addresses.Skip(index).Take(1).Single();
                Free(key.Key);
                return true;
            }
            return false;
        }

        public new IEnumerator<TValue> GetEnumerator()
        {
            foreach(var kvp in Addresses)
            {
                yield return Read<TValue>(kvp.Key);
            }
        }
    }

    public class PersistentList : PersistentHeap, IPersistentList
    {
        public PersistentList() : base()
        {
        }

        public PersistentList(string filePath, int maxSize = 1024 * 1024 * 1024) : base(filePath, maxSize)
        {

        }

        public object this[int index]
        {
            get
            {
                var key = Addresses.Skip(index).Take(1).Single();
                return Read(key.Key);
            }
            set
            {
                var key = Addresses.Skip(index).Take(1).Single();
                Write(value, key.Key);
            }
        }

        public bool IsFixedSize
        {
            get
            {
                return false;
            }
        }

        public bool Contains(object value)
        {
            foreach(var item in this)
            {
                if ((value == null && item == null) 
                    ||(value != null && value.Equals(item)))
                {
                    return true;
                }
            }
            return false;
        }

        public int IndexOf(object value)
        {
            var i = 0;
            foreach (var item in this)
            {
                if ((value == null && item == null)
                    || (value != null && value.Equals(item)))
                {
                    return i;
                }
                i++;
            }
            return -1;
        }

        public void Insert(int index, object value)
        {
            throw new NotSupportedException("Insert operations are not supported on this collection type");
        }

        public void Remove(object value)
        {
            var index = IndexOf(value);
            if (index >= 0)
            {
                var key = Addresses.Skip(index).Take(1).Single();
                Free(key.Key);
            }
        }

        public void RemoveAt(int index)
        {
            var key = Addresses.Skip(index).Take(1).Single();
            Free(key.Key);
        }

        int IList.Add(object value)
        {
            return (int)base.Add(value);
        }
    }
}
