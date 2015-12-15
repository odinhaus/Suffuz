using Altus.Suffūz.Collections.IO;
using Altus.Suffūz.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace Altus.Suffūz.Collections
{
    public unsafe class PersistentTransaction : BytePointerAdapter, IEnlistmentNotification, IDisposable
    {
        protected const int WAL_BLOCK_SIZE = 512;
        protected const int WAL_RECORD_TYPE = 0;
        protected const int WAL_SEQ_NO = 4;
        protected const int WAL_HEAP_SEQ_NO = 12;
        protected const int WAL_HEAP_ITEM_ADDRESS = 20;
        protected const int WAL_ITEM_ISVALID = 28;
        protected const int WAL_ITEM_LENGTH = 29;
        protected const int WAL_ITEM_TYPE = 32;
        protected const int WAL_ITEM_MD5 = 37;
        protected const int WAL_ITEM_DATA = 53;
        protected const int WAL_ITEM_DATA_LENGTH = WAL_BLOCK_SIZE - WAL_ITEM_DATA;

        [ThreadStatic]
        static PersistentTransaction _base;
        [ThreadStatic]
        static FileStream _walFile;
        [ThreadStatic]
        static List<IPersistentCollection> _flushables = new List<IPersistentCollection>();

        public PersistentTransaction(ref byte* ptr, long start, long end, IPersistentCollection flushable)
            :base(ref ptr, start, end)
        {
            this.Collection = flushable;
            this.TransactionScope = new TransactionScope();
            this.HasError = false;

            if (_flushables.Count == 0)
            {
                _base = this;
                LoadWAL();
                WriteTxBegin();
            }

            if (!_flushables.Contains(flushable))
            {
                _flushables.Add(flushable);
            }
        }

        public IPersistentCollection Collection { get; private set; }
        public bool HasError { get; private set; }

        protected virtual void LoadWAL()
        {
            var walFile = "$journal.wal";
            this.WALFilePath = walFile;
            _walFile = new FileStream(this.WALFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete, 1024 * 8, false);
        }

        /*
        public virtual void CheckConsistency()
        {
            lock (_scopes)
            {
                // Item                 Position    Type
                //===========================================
                // WAL Record Type      0 - 3       int
                // WAL Sequence No      4 - 11       ulong
                // Heap Sequence No     12 - 19     ulong
                // Heap Item Address    20 - 27     ulong
                // Heap Item IsValid    28          bool
                // Heap Item Length     29 - 32     int
                // Heap Item Type Code  32 - 36     int
                // Heap Item Data MD5   37 - 52     byte[]
                // Heap Item Data       53 - 511    byte[]

                var walLength = WALFile.Length - 512;
                var buffer = new byte[512 * 16]; // 8k read buffer = 16 WAL records @ 512 bytes per record

                ulong lastWALSequenceNo = 0, lastHeapSequenceNo = 0; // sequence numbers, as read from latest checkpoint (if found)
                int lastHeapAddress = HEAP_HEADER_LENGTH;

                // read the WAL backwards from the end, looking for most recent checkpoint record
                WALFile.Seek(0, SeekOrigin.End);
                do
                {
                    var bytesToRead = (int)Math.Min(buffer.Length, WALFile.Position);
                    WALFile.Seek(-bytesToRead, SeekOrigin.Current);
                    var read = WALFile.Read(buffer, 0, bytesToRead);
                    var blocks = read / 512;
                    WALFile.Seek(-bytesToRead, SeekOrigin.Current);

                    for (int i = blocks - 1; i >= 0; i--)
                    {
                        var ptr = i * 512;
                        var recordType = (WALEntryType)BitConverter.ToInt32(buffer, ptr + WAL_RECORD_TYPE);
                        if (recordType == WALEntryType.Checkpoint)
                        {
                            lastWALSequenceNo = BitConverter.ToUInt64(buffer, ptr + WAL_SEQ_NO);
                            lastHeapSequenceNo = BitConverter.ToUInt64(buffer, ptr + WAL_HEAP_SEQ_NO);
                            lastHeapAddress = (int)BitConverter.ToUInt64(buffer, ptr + WAL_HEAP_ITEM_ADDRESS);
                            TruncateWAL((int)(WALFile.Position + ptr + 512)); // this will set the WAL file position to 0, and exit the loop
                            Next = lastHeapAddress;
                            HeapSequenceNumber = lastHeapSequenceNo;
                            WALSequenceNumber = lastWALSequenceNo;
                            break;
                        }
                    }

                } while (WALFile.Position > 0);

                // now, read from front of truncated WAL file, verifying the current heap against change records in the WAL
                do
                {
                    var bytesToRead = (int)Math.Min(buffer.Length, WALFile.Length - WALFile.Position);
                    var read = WALFile.Read(buffer, 0, bytesToRead);
                    var blocks = read / 512;

                    for (int i = 0; i < blocks; i++)
                    {
                        var ptr = i * 512;
                        var recordType = (WALEntryType)BitConverter.ToInt32(buffer, ptr + WAL_RECORD_TYPE);
                        var walSeqNo = BitConverter.ToUInt64(buffer, ptr + WAL_SEQ_NO);
                        var heapSeqNo = BitConverter.ToUInt64(buffer, ptr + WAL_HEAP_SEQ_NO);
                        var heapAddress = (int)BitConverter.ToUInt64(buffer, ptr + WAL_HEAP_ITEM_ADDRESS);
                        var isValid = BitConverter.ToBoolean(buffer, ptr + WAL_ITEM_ISVALID);

                        var checkHeapIsValid = _ptr.ReadBoolean(heapAddress + ITEM_ISVALID); // record is valid
                        var checkHeapSeqNo = _ptr.ReadUInt64(heapAddress + ITEM_INDEX); // heap sequence number
                        var isGood = checkHeapIsValid == isValid && checkHeapSeqNo == heapSeqNo;

                        if (isGood)
                        {
                            if (isValid)
                            {
                                // check data lengths
                                var dataLen = BitConverter.ToInt32(buffer, ptr + WAL_ITEM_LENGTH);
                                var checkDataLen = _ptr.ReadInt32(heapAddress + ITEM_LENGTH);
                                isGood = dataLen == checkDataLen;
                                if (isGood)
                                {
                                    // we also need to check MD5 hashes for validity
                                    var md5Hash = new byte[16];
                                    Buffer.BlockCopy(buffer, ptr + WAL_ITEM_MD5, md5Hash, 0, 16);
                                    var checkMd5Hash = _hasher.ComputeHash(_ptr.ReadBytes(heapAddress + ITEM_DATA, checkDataLen));
                                    isGood = md5Hash.SequenceEqual(checkMd5Hash);
                                    if (!isGood && dataLen < WAL_ITEM_DATA_LENGTH)
                                    {
                                        // we can recover the data because we have a copy in the log
                                        _ptr.Write(heapAddress + ITEM_TYPE, BitConverter.ToInt32(buffer, ptr + WAL_ITEM_TYPE)); // write item's type
                                        var data = new byte[dataLen];
                                        Buffer.BlockCopy(buffer, ptr + WAL_ITEM_DATA, data, 0, dataLen);
                                        _ptr.Write(heapAddress + ITEM_DATA, data);
                                        isGood = true; // we corrected the error, move to the next one
                                    }
                                    heapAddress += ITEM_DATA + dataLen;
                                }
                            }

                        }

                        if (isGood)
                        {
                            // records match, heap is good from this point
                            if (heapAddress > lastHeapAddress)
                                lastHeapAddress = heapAddress;
                            if (heapSeqNo > lastHeapSequenceNo)
                                lastHeapSequenceNo = heapSeqNo;
                            lastWALSequenceNo = walSeqNo;
                        }
                        else
                        {
                            // records don't match, so heap is corrupted from this point
                            // truncate tail of WAL file, and bail
                            WALFile.SetLength(ptr); // cuts off everything from after where we're at
                        }
                    }

                    WALFile.Seek(bytesToRead, SeekOrigin.Current);
                } while (WALFile.Position < WALFile.Length - 1);

                WALSequenceNumber = lastWALSequenceNo;
                HeapSequenceNumber = lastHeapSequenceNo;
                Next = lastHeapAddress;

                UpdateHeaders();
                Compact();
                // forces a new WAL checkpoint to be written
                _walChanged = true;
                var rerun = WALFile.Length != walLength;
                CommitWAL(HeapSequenceNumber, Next); // add a checkpoint to where we are now
                if (rerun)
                {
                    // this will prune any additional replayed updates applied after the last checkpoint from the WAL file
                    CheckConsistency();
                }
            }
        }
        */

        public string WALFilePath { get; private set; }
        public FileStream WALFile { get { return _walFile; } }
        public ulong WALSequenceNumber { get; private set; }

        public BytePointerAdapter Pointer { get; set; }
        public TransactionScope TransactionScope { get; private set; }

        #region IDisposable Members
        bool disposed = false;

        // Implement IDisposable.
        // Do not make this method virtual.
        // A derived class should not be able to override this method.
        public void Dispose()
        {
            Dispose(true);
            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SupressFinalize to
            // take this object off the finalization queue 
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        public event EventHandler Disposing;
        public event EventHandler Disposed;
        //========================================================================================================//
        // Dispose(bool disposing) executes in two distinct scenarios.
        // If disposing equals true, the method has been called directly
        // or indirectly by a user's code. Managed and unmanaged resources
        // can be disposed.
        // If disposing equals false, the method has been called by the 
        // runtime from inside the finalizer and you should not reference 
        // other objects. Only unmanaged resources can be disposed.
        private void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!this.disposed)
            {
                if (this.Disposing != null)
                    this.Disposing(this, new EventArgs());
                // If disposing equals true, dispose all managed 
                // and unmanaged resources.
                if (disposing)
                {
                    if (!HasError)
                        TransactionScope.Complete();
                    TransactionScope.Dispose();
                    TransactionScope = null;
                    
                    // Dispose subclass managed resources.
                    this.OnDisposeManagedResources();
                }

                // Call the appropriate methods to clean up 
                // unmanaged resources here.
                // If disposing is false, 
                // only the following code is executed.
                this.OnDisposeUnmanagedResources();
                if (this.Disposed != null)
                    this.Disposed(this, new EventArgs());
            }
            disposed = true;
        }

        /// <summary>
        /// Dispose managed resources
        /// </summary>
        protected virtual void OnDisposeManagedResources()
        {
        }

        /// <summary>
        /// Dispose unmanaged (native resources)
        /// </summary>
        protected virtual void OnDisposeUnmanagedResources()
        {
        }

        #endregion


        #region IEnlistmentNotification Members
        public void Commit(Enlistment enlistment)
        {
            if (_flushables.Count > 0)
            {
                WALFile.Close();
                File.Delete(WALFilePath);
            }
            enlistment.Done();
        }

        public void InDoubt(Enlistment enlistment)
        {
            if (_flushables.Count > 0)
            {
            }
            WriteTxRollback();
            enlistment.Done();
        }

        public void Prepare(PreparingEnlistment preparingEnlistment)
        {
            if (_flushables.Count > 0)
            {
                try
                {
                    foreach (var flushable in _flushables)
                    {
                        flushable.Flush();
                    }
                    WriteTxCommit();
                    preparingEnlistment.Prepared();
                }
                catch
                {
                    WriteTxRollback();
                    preparingEnlistment.ForceRollback();
                }
                finally
                {
                    _flushables.Clear();
                }
            }
            else
            {
                preparingEnlistment.Prepared();
            }
        }

        public void Rollback(Enlistment enlistment)
        {
            if (_flushables.Count > 0)
            {
            }
            enlistment.Done();
            _flushables.Clear();
        }
        #endregion


        #region BytePointerAdapter Overrides
        public override void Write(int position, byte[] value)
        {
            try
            {
                using (var tx = new TransactionScope())
                {
                    Transaction.Current.EnlistVolatile(this, EnlistmentOptions.None);
                    WriteCurrent(position, ReadBytes(position, value.Length));
                    WriteNew(position, value);
                    base.Write(position, value);
                    tx.Complete();
                }
            }
            catch
            {
                HasError = true;
                throw;
            }
        }

        public override void Write(int position, byte[] value, int start, int length)
        {
            try
            {
                using (var tx = new TransactionScope())
                {
                    Transaction.Current.EnlistVolatile(this, EnlistmentOptions.None);
                    WriteCurrent(position, ReadBytes(position, length));
                    WriteNew(position, value.Skip(start).Take(length).ToArray());
                    base.Write(position, value, start, length);
                    tx.Complete();
                }
            }
            catch
            {
                HasError = true;
                throw;
            }
        }

        public override void Write(long position, bool value)
        {
            try
            {
                using (var tx = new TransactionScope())
                {
                    Transaction.Current.EnlistVolatile(this, EnlistmentOptions.None);
                    WriteCurrent(position, ReadBytes(position, 1));
                    WriteNew(position, BitConverter.GetBytes(value));
                    base.Write(position, value);
                    tx.Complete();
                }
            }
            catch
            {
                HasError = true;
                throw;
            }
        }

        public override void Write(long position, byte value)
        {
            try
            {
                using (var tx = new TransactionScope())
                {
                    Transaction.Current.EnlistVolatile(this, EnlistmentOptions.None);
                    WriteCurrent(position, ReadBytes(position, 1));
                    WriteNew(position, new byte[] { value });
                    base.Write(position, value);
                    tx.Complete();
                }
            }
            catch
            {
                HasError = true;
                throw;
            }
        }

        public override void Write(long position, char value)
        {
            try
            {
                using (var tx = new TransactionScope())
                {
                    Transaction.Current.EnlistVolatile(this, EnlistmentOptions.None);
                    WriteCurrent(position, ReadBytes(position, 2));
                    WriteNew(position, BitConverter.GetBytes(value));
                    base.Write(position, value);
                    tx.Complete();
                }
            }
            catch
            {
                HasError = true;
                throw;
            }
        }

        public override void Write(long position, DateTime value)
        {
            try
            {
                using (var tx = new TransactionScope())
                {
                    Transaction.Current.EnlistVolatile(this, EnlistmentOptions.None);
                    WriteCurrent(position, ReadBytes(position, 8));
                    WriteNew(position, BitConverter.GetBytes(value.ToBinary()));
                    base.Write(position, value);
                    tx.Complete();
                }
            }
            catch
            {
                HasError = true;
                throw;
            }
        }

        public override void Write(long position, decimal value)
        {
            try
            {
                using (var tx = new TransactionScope())
                {
                    Transaction.Current.EnlistVolatile(this, EnlistmentOptions.None);
                    WriteCurrent(position, ReadBytes(position, 16));
                    WriteNew(position, value.GetBytes());
                    base.Write(position, value);
                    tx.Complete();
                }
            }
            catch
            {
                HasError = true;
                throw;
            }
        }

        public override void Write(long position, double value)
        {
            try
            {
                using (var tx = new TransactionScope())
                {
                    Transaction.Current.EnlistVolatile(this, EnlistmentOptions.None);
                    WriteCurrent(position, ReadBytes(position, 8));
                    WriteNew(position, BitConverter.GetBytes(value));
                    base.Write(position, value);
                    tx.Complete();
                }
            }
            catch
            {
                HasError = true;
                throw;
            }
        }

        public override void Write(long position, float value)
        {
            try
            {
                using (var tx = new TransactionScope())
                {
                    Transaction.Current.EnlistVolatile(this, EnlistmentOptions.None);
                    WriteCurrent(position, ReadBytes(position, 4));
                    WriteNew(position, BitConverter.GetBytes(value));
                    base.Write(position, value);
                    tx.Complete();
                }
            }
            catch
            {
                HasError = true;
                throw;
            }
        }
        public override void Write(long position, int value)
        {
            try
            {
                using (var tx = new TransactionScope())
                {
                    Transaction.Current.EnlistVolatile(this, EnlistmentOptions.None);
                    WriteCurrent(position, ReadBytes(position, 4));
                    WriteNew(position, BitConverter.GetBytes(value));
                    base.Write(position, value);
                    tx.Complete();
                }
            }
            catch
            {
                HasError = true;
                throw;
            }
        }
        public override void Write(long position, long value)
        {
            try
            {
                using (var tx = new TransactionScope())
                {
                    Transaction.Current.EnlistVolatile(this, EnlistmentOptions.None);
                    WriteCurrent(position, ReadBytes(position, 8));
                    WriteNew(position, BitConverter.GetBytes(value));
                    base.Write(position, value);
                    tx.Complete();
                }
            }
            catch
            {
                HasError = true;
                throw;
            }
        }

        public override void Write(long position, short value)
        {
            try
            {
                using (var tx = new TransactionScope())
                {
                    Transaction.Current.EnlistVolatile(this, EnlistmentOptions.None);
                    WriteCurrent(position, ReadBytes(position, 2));
                    WriteNew(position, BitConverter.GetBytes(value));
                    base.Write(position, value);
                    tx.Complete();
                }
            }
            catch
            {
                HasError = true;
                throw;
            }
        }

        public override void Write(long position, uint value)
        {
            try
            {
                using (var tx = new TransactionScope())
                {
                    Transaction.Current.EnlistVolatile(this, EnlistmentOptions.None);
                    WriteCurrent(position, ReadBytes(position, 4));
                    WriteNew(position, BitConverter.GetBytes(value));
                    base.Write(position, value);
                    tx.Complete();
                }
            }
            catch
            {
                HasError = true;
                throw;
            }
        }

        public override void Write(long position, ulong value)
        {
            try
            {
                using (var tx = new TransactionScope())
                {
                    Transaction.Current.EnlistVolatile(this, EnlistmentOptions.None);
                    WriteCurrent(position, ReadBytes(position, 8));
                    WriteNew(position, BitConverter.GetBytes(value));
                    base.Write(position, value);
                    tx.Complete();
                }
            }
            catch
            {
                HasError = true;
                throw;
            }
        }

        public override void Write(long position, ushort value)
        {
            try
            {
                using (var tx = new TransactionScope())
                {
                    Transaction.Current.EnlistVolatile(this, EnlistmentOptions.None);
                    WriteCurrent(position, ReadBytes(position, 2));
                    WriteNew(position, BitConverter.GetBytes(value));
                    base.Write(position, value);
                    tx.Complete();
                }
            }
            catch
            {
                HasError = true;
                throw;
            }
        }

        protected virtual void WriteCurrent(long position, byte[] value)
        {
            try
            {
                lock (Collection)
                {
                    // byte - type (0 = current, 1 = new)
                    // int - Collection Index
                    // long - address
                    // int - length
                    // byte[] - data
                    WALFile.Write((byte)10);
                    WriteTx(position, value);
                }
            }
            catch
            {
                HasError = true;
                throw;
            }
        }

        protected virtual void WriteNew(long position, byte[] value)
        {
            try
            {
                lock (Collection)
                {
                    // byte - type (0 = TxBegin, TxCommit = 1, TxRollback = 2, 10 = current value, 11 = new)
                    // int - Collection Index
                    // long - address
                    // int - length
                    // byte[] - data
                    WALFile.Write((byte)11);
                    WriteTx(position, value);
                }
            }
            catch
            {
                HasError = true;
                throw;
            }
        }

        protected virtual void WriteTxBegin()
        {
            try
            {
                lock (Collection)
                {
                    // byte - type (0 = TxBegin, TxCommit = 1, TxRollback = 2, 10 = current value, 11 = new)
                    // int - Collection Index
                    // long - address
                    // int - length
                    // byte[] - data
                    WALFile.Write((byte)0);
                }
            }
            catch
            {
                HasError = true;
                throw;
            }
        }

        protected virtual void WriteTxCommit()
        {
            try
            {
                lock (Collection)
                {
                    // byte - type (0 = TxBegin, TxCommit = 1, TxRollback = 2, 10 = current value, 11 = new)
                    // int - Collection Index
                    // long - address
                    // int - length
                    // byte[] - data
                    WALFile.Write((byte)1);
                }
            }
            catch
            {
                HasError = true;
                throw;
            }
        }

        protected virtual void WriteTxRollback()
        {
            try
            {
                lock (Collection)
                {
                    // byte - type (0 = TxBegin, TxCommit = 1, TxRollback = 2, 10 = current value, 11 = new)
                    // int - Collection Index
                    // long - address
                    // int - length
                    // byte[] - data
                    WALFile.Write((byte)2);
                }
            }
            catch
            {
                HasError = true;
                throw;
            }
        }

        protected virtual void WriteTx(long position, byte[] value)
        {
            WALFile.Write(position);
            WALFile.Write(value.Length);
            WALFile.Write(value);
        }
        #endregion
    }
}
