using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Collections
{
    public class PersistentQueue<TValue> : PersistentQueue, IPersistentQueue<TValue>
    {
        public PersistentQueue() : base()
        {
        }

        public PersistentQueue(string filePath, int maxSize = 1024 * 1024 * 1024) : base(filePath, maxSize)
        {

        }

        public void Enqueue(TValue item)
        {
            base.Enqueue(item);
        }

        public new TValue Dequeue()
        {
            return (TValue) base.Dequeue();
        }

        public bool TryDequeue(out TValue item)
        {
            object o;
            var ret = base.TryDequeue(out o);

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

    public class PersistentQueue : PersistentHeap, IPersistentQueue
    {
        public PersistentQueue() : base()
        {
        }

        public PersistentQueue(string filePath, int maxSize = 1024 * 1024 * 1024) : base(filePath, maxSize)
        {

        }

        public bool TryDequeue(out object item)
        {
            lock (SyncRoot)
            {
                if (this.Count > 0)
                {
                    var key = Addresses.First().Key;
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

        public object Dequeue()
        {
            lock(SyncRoot)
            {
                if (this.Count > 0)
                {
                    var key = Addresses.First().Key;
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
            lock(SyncRoot)
            {
                if (this.Count > 0)
                {
                    var key = Addresses.First().Key;
                    return Read(key);
                }
                else
                {
                    return null;
                }
            }
        }

        public void Enqueue(object item)
        {
            Add(item);
        }
    }
}
