using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public abstract class ChangeState : IList
    {
        object _syncRoot = new object();

        [BinarySerializable(0)]
        public ulong Epoch { get; set; }
        [BinarySerializable(1)]
        public string ObservableId { get; set; }
        [BinarySerializable(2)]
        public string PropertyName { get; set; }
        public abstract Type ValueType { get; }

        public abstract object this[int index]
        {
            get;

            set;
        }

        public abstract int Count
        {
            get;
        }
        public bool IsFixedSize
        {
            get
            {
                return false;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public bool IsSynchronized
        {
            get
            {
                return false;
            }
        }

        public object SyncRoot
        {
            get
            {
                return _syncRoot;
            }
        }

        public abstract int Add(object value);

        public abstract void Clear();

        public abstract bool Contains(object value);

        public abstract void CopyTo(Array array, int index);

        public abstract IEnumerator GetEnumerator();

        public abstract int IndexOf(object value);

        public abstract void Insert(int index, object value);

        public abstract void Remove(object value);

        public abstract void RemoveAt(int index);
    }

    public class ChangeState<T> : ChangeState, IList<Change<T>>
    {
        private List<Change<T>> _changes = new List<Change<T>>();

        public override Type ValueType
        {
            get
            {
                return typeof(T);
            }
        }

        public override int Count
        {
            get
            {
                return _changes.Count;
            }
        }

        Change<T> IList<Change<T>>.this[int index]
        {
            get
            {
                return ((IList<Change<T>>)_changes)[index];
            }

            set
            {
                ((IList<Change<T>>)_changes)[index] = value;
            }
        }

        public override object this[int index]
        {
            get
            {
                return _changes[index];
            }

            set
            {
                _changes[index] = (Change<T>)value;
            }
        }

        public void Add(Change<T> item)
        {
            _changes.Add(item);
        }

        public override void Clear()
        {
            _changes.Clear();
        }

        public bool Contains(Change<T> item)
        {
            return _changes.Contains(item);
        }

        public void CopyTo(Change<T>[] array, int arrayIndex)
        {
            _changes.CopyTo(array, arrayIndex);
        }

        public int IndexOf(Change<T> item)
        {
            return _changes.IndexOf(item);
        }

        public void Insert(int index, Change<T> item)
        {
            _changes.Insert(index, item);
        }

        public bool Remove(Change<T> item)
        {
            return _changes.Remove(item);
        }

        public override void RemoveAt(int index)
        {
            _changes.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override int Add(object value)
        {
            this.Add((Change<T>)value);
            return this.Count;
        }

        public override bool Contains(object value)
        {
            return this.Contains((Change<T>)value);
        }

        public override void CopyTo(Array array, int index)
        {
            this.CopyTo((Change<T>[])array, index);
        }

        public override int IndexOf(object value)
        {
            return this.IndexOf((Change<T>)value);
        }

        public override void Insert(int index, object value)
        {
            this.Insert(index, (Change<T>)value);
        }

        public override void Remove(object value)
        {
            this.Remove((Change<T>)value);
        }

        public override IEnumerator GetEnumerator()
        {
            return _changes.GetEnumerator();
        }

        IEnumerator<Change<T>> IEnumerable<Change<T>>.GetEnumerator()
        {
            return ((IList<Change<T>>)_changes).GetEnumerator();
        }
    }
}
