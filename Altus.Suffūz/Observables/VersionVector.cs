using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public class VersionVector<T> : IList<VersionVectorEntry<T>>
    {
        List<VersionVectorEntry<T>> _list = new List<VersionVectorEntry<T>>();

        public VersionVectorEntry<T> this[int index]
        {
            get
            {
                return _list[index];
            }

            set
            {
                _list[index] = value;
            }
        }

        public int Count
        {
            get
            {
                return _list.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public void Add(VersionVectorEntry<T> item)
        {
            _list.Add(item);
        }

        public void Clear()
        {
            _list.Clear();
        }

        public bool Contains(VersionVectorEntry<T> item)
        {
            return _list.Contains(item);
        }

        public void CopyTo(VersionVectorEntry<T>[] array, int arrayIndex)
        {
            _list.CopyTo(array, arrayIndex);
        }

        public IEnumerator<VersionVectorEntry<T>> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        public int IndexOf(VersionVectorEntry<T> item)
        {
            return _list.IndexOf(item);
        }

        public void Insert(int index, VersionVectorEntry<T> item)
        {
            _list.Insert(index, item);
        }

        public bool Remove(VersionVectorEntry<T> item)
        {
            return _list.Remove(item);
        }

        public void RemoveAt(int index)
        {
            _list.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
