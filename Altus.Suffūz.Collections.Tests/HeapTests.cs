using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using Altus.Suffūz.Collections.IO;

namespace Altus.Suffūz.Collections.Tests
{
    [TestClass]
    public class HeapTests
    {
        [TestMethod]
        public void CanCreateHeap()
        {
            var fileName = "Heap.loh";
            File.Delete(fileName);
            using (var heap = new PersistentHeap(fileName, 1024 * 64))
            {
                Assert.IsTrue(heap.Length == 20);
            }
            File.Delete(fileName);
        }

        [TestMethod]
        public void CanWriteObjectHeap()
        {
            var fileName = "Heap.loh";
            File.Delete(fileName);
            using (var heap = new PersistentHeap(fileName, 1024 * 64))
            {
                var item = new CustomItem() { A = 12, B = "Foo" };

                var address1 = heap.Write(item);
                var address2 = heap.Write(item);
            }
            File.Delete(fileName);
        }

        [TestMethod]
        public void CanReadObjectHeap()
        {
            var fileName = "Heap.loh";
            using (var heap = new PersistentHeap(fileName, 1024 * 64))
            {
                var itemA = new CustomItem() { A = 12, B = "A" };
                var itemB = new CustomItem() { A = 22, B = "B" };

                var address1 = heap.Write(itemA);
                var address2 = heap.Write(itemB);

                var item1 = (CustomItem)heap.Read(address1);
                var item2 = (CustomItem)heap.Read(address2);

                Assert.IsTrue(itemA.A == item1.A && itemA.B == item1.B);
                Assert.IsTrue(itemB.A == item2.A && itemB.B == item2.B);
            }
            File.Delete(fileName);
        }


        [TestMethod]
        public void CanReadStoredObjectHeap()
        {
            var fileName = "Heap.loh";
            var itemA = new CustomItem() { A = 12, B = "A" };
            var itemB = new CustomItem() { A = 22, B = "B" };
            ulong address1 = 0;
            ulong address2 = 0;

            using (var heap = new PersistentHeap(fileName, 1024 * 64))
            {
                address1 = heap.Write(itemA);
                address2 = heap.Write(itemB);
            }

            using (var heap = new PersistentHeap(fileName, 1024 * 128))
            {
                var item1 = (CustomItem)heap.Read(address1);
                var item2 = (CustomItem)heap.Read(address2);

                Assert.IsTrue(itemA.A == item1.A && itemA.B == item1.B);
                Assert.IsTrue(itemB.A == item2.A && itemB.B == item2.B);
            }

            File.Delete(fileName);
        }

        [TestMethod]
        public void MeetsPerfCharacteristicsComplex()
        {
            var fileName = "Heap.loh";
            float writeRate, readRate, loadRate, enumerateRate;
            var count = 1000000;
            var sw = new Stopwatch();
            var item = new CustomItem() { A = 12, B = "some text here" };
            File.Delete(fileName);
            using (var scope = new FlushScope())
            {
                using (var heap = new PersistentHeap(fileName, 1024 * 1024 * 100))
                {
                    var addresses = new ulong[count];
                    sw.Start();
                    for (int i = 0; i < count; i++)
                    {
                        addresses[i] = heap.Write(item);
                    }
                    sw.Stop();
                    writeRate = (float)count / (sw.ElapsedMilliseconds / 1000f);

                    sw.Reset();
                    sw.Start();
                    for (int i = 0; i < count; i++)
                    {
                        heap.Read(addresses[i]);
                    }
                    sw.Stop();
                    readRate = (float)count / (sw.ElapsedMilliseconds / 1000f);

                    sw.Reset();
                    sw.Start();
                    foreach (var thing in heap)
                    {
                    }
                    sw.Stop();
                    enumerateRate = (float)count / (sw.ElapsedMilliseconds / 1000f);
                }
            }

            sw.Start();
            using (var scope = new FlushScope())
            {
                sw.Stop();
            }
            loadRate = (float)count / (sw.ElapsedMilliseconds / 1000f);

            File.Delete(fileName);
            Assert.Inconclusive("Write Rate: {0}, Read Rate: {1}, Load Rate: {2}, Enumerate Rate: {3}", writeRate, readRate, loadRate, enumerateRate);
        }


        [TestMethod]
        public void MeetsPerfCharacteristicsSimple()
        {
            var fileName = "Heap.loh";
            float writeRate, readRate, loadRate, enumerateRate;
            var count = 1000000;
            var sw = new Stopwatch();
            File.Delete(fileName);
            using (var scope = new FlushScope())
            {
                using (var heap = new PersistentHeap(fileName, 1024 * 1024 * 100))
                {
                    var addresses = new ulong[count];
                    sw.Start();
                    for (int i = 0; i < count; i++)
                    {
                        addresses[i] = heap.Write(i);
                    }
                    sw.Stop();
                    writeRate = (float)count / (sw.ElapsedMilliseconds / 1000f);

                    sw.Reset();
                    sw.Start();
                    for (int i = 0; i < count; i++)
                    {
                        heap.Read(addresses[i]);
                    }
                    sw.Stop();
                    readRate = (float)count / (sw.ElapsedMilliseconds / 1000f);

                    sw.Reset();
                    sw.Start();
                    foreach (var item in heap)
                    {
                    }
                    sw.Stop();
                    enumerateRate = (float)count / (sw.ElapsedMilliseconds / 1000f);
                }
            }

            sw.Start();
            using (var scope = new FlushScope())
            {
                sw.Stop();
            }
            loadRate = (float)count / (sw.ElapsedMilliseconds / 1000f);

            File.Delete(fileName);
            Assert.Inconclusive("Write Rate: {0}, Read Rate: {1}, Load Rate: {2}, Enumerate Rate: {3}", writeRate, readRate, loadRate, enumerateRate);
        }

        [TestMethod]
        [ExpectedException(typeof(KeyNotFoundException))]
        public void CanRemoveItem()
        {
            var fileName = "Heap.loh";
            File.Delete(fileName);
            using (var heap = new PersistentHeap(fileName, 1024 * 1024 * 100))
            {
                var address = heap.Write(14);
                heap.Free(address);
                var value = (int)heap.Read(address);
            }
            File.Delete(fileName);
        }

        [TestMethod]
        public void CanCompactHeap()
        {
            var fileName = "Heap.loh";
            File.Delete(fileName);
            using (var heap = new PersistentHeap(fileName))
            {
                var key = heap.Write(14);
                heap.Free(key);
                key = heap.Write(15);
                key = heap.Write(16);   
                key = heap.Write(17);
                heap.Free(key);
                key = heap.Write(true);
                key = heap.Write(new CustomItem() { A = 14, B = "Some crzy text" });
                key = heap.Write(new CustomItem() { A = 14, B = "Some more crzy text" });
                heap.Free(key);
                var heapSize = heap.Length;
                heap.Compact();
                Assert.IsTrue(heap.Read<int>(2) == 15);
                Assert.IsTrue(heap.Read<int>(3) == 16);
                Assert.IsTrue(heap.Read<bool>(5) == true);
                Assert.IsTrue(heap.Read<CustomItem>(6).B == "Some crzy text");
                Assert.IsTrue(heapSize - heap.Length == 84);
            }
            File.Delete(fileName);
        }

        [TestMethod]
        public void FailsPerfCharacteristicsWithoutFlushScope()
        {
            var fileName = "Heap.loh";
            File.Delete(fileName);
            using (var heap = new PersistentHeap(fileName))
            {
                var sw = new Stopwatch();
                var count = 1000;
                var addresses = new ulong[count];
                sw.Start();
                for (int i = 0; i < count; i++)
                {
                    addresses[i] = heap.Write(i);
                }
                sw.Stop();
                var writeRate = (float)count / (sw.ElapsedMilliseconds / 1000f);

                sw.Start();
                for (int i = 0; i < count; i++)
                {
                    heap.Read(addresses[i]);
                }
                sw.Stop();
                var readRate = (float)count / (sw.ElapsedMilliseconds / 1000f);

                Assert.Inconclusive("Write Rate: {0}, Read Rate: {1}", writeRate, readRate);
            }

            File.Delete(fileName);
        }


        [TestMethod]
        public void CanLinqToGenericHeap()
        {
            var fileName = "Heap.loh";
            File.Delete(fileName);
            using (var heap = new PersistentHeap<CustomItem>(fileName))
            {
                using (var scope = new FlushScope())
                {
                    for (int i = 0; i < 10000; i++)
                    {
                        heap.Write(new CustomItem() { A = i, B = "Some text" });
                    }
                }

                var results = heap.Where(i => i.A < 10);
                Assert.IsTrue(results.Count() == 10);
            }

            File.Delete(fileName);
        }

        [TestMethod]
        public void CanOverwriteHeapItem()
        {
            var fileName = "Heap.loh";
            File.Delete(fileName);
            using (var heap = new PersistentHeap<CustomItem>(fileName))
            {
                var key = heap.Write(new CustomItem() { A = 100, B = "Some text" });
                var length = heap.Length;
                var current = heap.Read(key);
                var key2 = heap.OverwriteUnsafe(new CustomItem() { A = 200, B = "Some text" }, key);
                var edited = heap.Read(key2);
                var editedLength = heap.Length;

                Assert.IsTrue(key == key2);
                Assert.IsTrue(current.A == 100 && edited.A == 200);
                Assert.IsTrue(length == editedLength);
            }

            File.Delete(fileName);
        }
    }
}
