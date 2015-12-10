using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Collections
{
    public class PersistentStack<TValue> : PersistentStack, IPersistentStack<TValue>
    {
        public PersistentStack() : base()
        {
        }

        public PersistentStack(string filePath, int maxSize = 1024 * 1024 * 1024) : base(filePath, maxSize)
        {

        }

        public void Push(TValue item)
        {
            base.Push(item);
        }

        public new TValue Pop()
        {
            return (TValue) base.Pop();
        }

        public bool TryPop(out TValue item)
        {
            object o;
            var ret = base.TryPop(out o);

            if (ret)
            {
                item = (TValue)o;
            }
            else
            {
                item = default(TValue);
            }
            return ret;
        }

        public new TValue Peek()
        {
            var item = base.Peek();
            if (item == null)
                return default(TValue);
            else
                return (TValue)item;
        }
    }

    public class PersistentStack : PersistentHeap, IPersistentStack
    {
        public PersistentStack() : base()
        {
        }

        public PersistentStack(string filePath, int maxSize = 1024 * 1024 * 1024) : base(filePath, maxSize)
        {

        }

        public bool TryPop(out object item)
        {
            lock (SyncRoot)
            {
                if (this.Count > 0)
                {
                    var key = Addresses.Last().Key;
                    item = Read(key);
                    Free(key);
                    return true;
                }
                else
                {
                    item = null;
                    return false;
                }
            }
        }

        public object Pop()
        {
            lock(SyncRoot)
            {
                if (this.Count > 0)
                {
                    var key = Addresses.Last().Key;
                    var item = Read(key);
                    Free(key);
                    if (Count == 0)
                    {
                        // move the write pointer back to the front of the heap, and overwrite old data
                        First = Last = 0;
                        Next = HEAP_HEADER_LENGTH;
                    }
                    return item;
                }
                else
                {
                    throw new InvalidOperationException("The queue is empty.");
                }
            }
        }

        public object Peek()
        {
            lock (SyncRoot)
            {
                if (this.Count > 0)
                {
                    var key = Addresses.Last().Key;
                    return Read(key);
                }
                else
                {
                    return null;
                }
            }
        }

        public void Push(object item)
        {
            Add(item);
        }
    }
}
