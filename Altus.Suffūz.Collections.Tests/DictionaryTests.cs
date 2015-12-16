using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace Altus.Suffūz.Collections.Tests
{
    [TestClass]
    public class DictionaryTests
    {
        [TestMethod]
        public void CanCreateDictionary()
        {
            var fileName = "Dictionary.dic";
            var keyName = Path.GetFileNameWithoutExtension(fileName) + "_keys.bin";
            File.Delete(fileName);
            File.Delete(keyName);
            using (var dictionary = new PersistentDictionary<string, CustomItem>(fileName, 1024 * 64))
            {
                Assert.IsTrue(dictionary.Count == 0);
                Assert.IsTrue(dictionary.AllowOverwrites == false);
                Assert.IsTrue(dictionary.IsReadOnly == false);
                Assert.IsTrue(dictionary.IsSynchronized == true);
                Assert.IsTrue(dictionary.MaximumSize == 1024 * 64 + 20);
            }
            File.Delete(fileName);
            File.Delete(keyName);
        }

        [TestMethod]
        public void CanAddItems()
        {
            var fileName = "Dictionary.dic";
            var keyName = Path.GetFileNameWithoutExtension(fileName) + "_keys.bin";
            File.Delete(fileName);
            File.Delete(keyName);
            using (var dictionary = new PersistentDictionary<string, CustomItem>(fileName, 1024 * 64))
            {
                dictionary.Add("some key 1", new CustomItem() { A = 10, B = "some text" });
                dictionary.Add("some key 2", new CustomItem() { A = 20, B = "some text" });
                dictionary.Add("some key 3", new CustomItem() { A = 30, B = "some text" });

                Assert.IsTrue(dictionary.Count == 3);
            }
            File.Delete(fileName);
            File.Delete(keyName);
        }

        [TestMethod]
        public void CanGetItems()
        {
            var fileName = "Dictionary.dic";
            var keyName = Path.GetFileNameWithoutExtension(fileName) + "_keys.bin";
            File.Delete(fileName);
            File.Delete(keyName);
            using (var dictionary = new PersistentDictionary<string, CustomItem>(fileName, 1024 * 64))
            {
                dictionary.Add("some key 1", new CustomItem() { A = 10, B = "some text" });
                dictionary.Add("some key 2", new CustomItem() { A = 20, B = "some text" });
                dictionary.Add("some key 3", new CustomItem() { A = 30, B = "some text" });

                Assert.IsTrue(dictionary["some key 1"].A == 10);
                Assert.IsTrue(dictionary["some key 2"].A == 20);
                Assert.IsTrue(dictionary["some key 3"].A == 30);
            }
            File.Delete(fileName);
            File.Delete(keyName);
        }

        [TestMethod]
        public void CanRemoveItems()
        {
            var fileName = "Dictionary.dic";
            var keyName = Path.GetFileNameWithoutExtension(fileName) + "_keys.bin";
            File.Delete(fileName);
            File.Delete(keyName);
            using (var dictionary = new PersistentDictionary<string, CustomItem>(fileName, 1024 * 64))
            {
                dictionary.Add("some key 1", new CustomItem() { A = 10, B = "some text" });
                dictionary.Add("some key 2", new CustomItem() { A = 20, B = "some text" });
                dictionary.Add("some key 3", new CustomItem() { A = 30, B = "some text" });

                dictionary.Remove("some key 1");
                dictionary.Remove("some key 2");
                dictionary.Remove("some key 3");

                Assert.IsTrue(dictionary.Count == 0);
            }
            File.Delete(fileName);
            File.Delete(keyName);
        }

        [TestMethod]
        public void CanCompact()
        {
            var fileName = "Dictionary.dic";
            var keyName = Path.GetFileNameWithoutExtension(fileName) + "_keys.bin";
            File.Delete(fileName);
            File.Delete(keyName);
            using (var dictionary = new PersistentDictionary<string, CustomItem>(fileName, 1024 * 64))
            {
                var length = dictionary.Length;

                dictionary.Add("some key 1", new CustomItem() { A = 10, B = "some text" });
                dictionary.Add("some key 2", new CustomItem() { A = 20, B = "some text" });
                dictionary.Add("some key 3", new CustomItem() { A = 30, B = "some text" });

                dictionary.Remove("some key 1");
                dictionary.Remove("some key 2");
                dictionary.Remove("some key 3");

                Assert.IsTrue(dictionary.Count == 0);

                dictionary.Compact();

                Assert.IsTrue(dictionary.Length == length);
            }
            File.Delete(fileName);
            File.Delete(keyName);
        }

        [TestMethod]
        public void CanGrow()
        {
            var fileName = "Dictionary.dic";
            var keyName = Path.GetFileNameWithoutExtension(fileName) + "_keys.bin";
            File.Delete(fileName);
            File.Delete(keyName);
            using (var dictionary = new PersistentDictionary<string, CustomItem>(fileName, 1024 * 64))
            {
                try
                {
                    using (var scope = new FlushScope())
                    {
                        var i = 0;
                        while (true)
                        {
                            dictionary.Add("some key " + i.ToString(), new CustomItem() { A = 10, B = "some text" });
                            i++;
                        }
                    }
                }
                catch(OutOfMemoryException)
                {
                    dictionary.Grow(1024 * 64);
                }

                dictionary.Add("some key X", new CustomItem() { A = 101, B = "some text" });
                Assert.IsTrue(dictionary["some key X"].A == 101);

            }
            File.Delete(fileName);
            File.Delete(keyName);
        }

        [TestMethod]
        public void CanClear()
        {
            var fileName = "Dictionary.dic";
            var keyName = Path.GetFileNameWithoutExtension(fileName) + "_keys.bin";
            File.Delete(fileName);
            File.Delete(keyName);
            using (var dictionary = new PersistentDictionary<string, CustomItem>(fileName, 1024 * 64))
            {
                try
                {
                    using (var scope = new FlushScope())
                    {
                        var i = 0;
                        while (true)
                        {
                            dictionary.Add("some key " + i.ToString(), new CustomItem() { A = 10, B = "some text" });
                            i++;
                        }
                    }
                }
                catch (OutOfMemoryException)
                {
                    dictionary.Clear();
                }

                Assert.IsTrue(dictionary.Count == 0);
                Assert.IsTrue(dictionary.Length > 20);
            }
            File.Delete(fileName);
            File.Delete(keyName);
        }

        [TestMethod]
        public void CanClearAndCompact()
        {
            var fileName = "Dictionary.dic";
            var keyName = Path.GetFileNameWithoutExtension(fileName) + "_keys.bin";
            File.Delete(fileName);
            File.Delete(keyName);
            using (var dictionary = new PersistentDictionary<string, CustomItem>(fileName, 1024 * 64))
            {
                try
                {
                    using (var scope = new FlushScope())
                    {
                        var i = 0;
                        while (true)
                        {
                            dictionary.Add("some key " + i.ToString(), new CustomItem() { A = 10, B = "some text" });
                            i++;
                        }
                    }
                }
                catch (OutOfMemoryException)
                {
                    dictionary.Clear(true);
                }

                Assert.IsTrue(dictionary.Count == 0);
                Assert.IsTrue(dictionary.Length == 20);
            }
            File.Delete(fileName);
            File.Delete(keyName);
        }

        [TestMethod]
        public void FailsPerfCharacteristicsWithoutFlushScope()
        {
            var fileName = "Dictionary.dic";
            var keyName = Path.GetFileNameWithoutExtension(fileName) + "_keys.bin";
            File.Delete(fileName);
            File.Delete(keyName);
            CustomItem item = new CustomItem() { A = 10, B = "some text" };
            using (var heap = new PersistentDictionary<string, CustomItem>(fileName))
            {
                var sw = new Stopwatch();
                var count = 1000;
                sw.Start();
                for (int i = 0; i < count; i++)
                {
                    heap.Add(i.ToString(), item);
                }
                sw.Stop();
                var writeRate = (float)count / (sw.ElapsedMilliseconds / 1000f);

                
                sw.Start();
                for (int i = 0; i < count; i++)
                {
                    item = heap[i.ToString()];
                }
                sw.Stop();
                var readRate = (float)count / (sw.ElapsedMilliseconds / 1000f);

                Assert.Inconclusive("Write Rate: {0}, Read Rate: {1}", writeRate, readRate);
            }

            File.Delete(fileName);
            File.Delete(keyName);
        }

        [TestMethod]
        public void MeetsPerfCharacteristicsComplex()
        {
            var fileName = "Dictionary.dic";
            var keyName = Path.GetFileNameWithoutExtension(fileName) + "_keys.bin";
            File.Delete(fileName);
            File.Delete(keyName);
            float writeRate, readRate, loadRate, enumerateRate;
            var count = 10000;
            var sw = new Stopwatch();
            var item = new CustomItem() { A = 12, B = "some text here" };
            using (var scope = new FlushScope())
            {
                using (var heap = new PersistentDictionary<string, CustomItem>(fileName, 1024 * 1024 * 100))
                {
                    using (var tx = new TransactionScope())
                    {
                        var addresses = new ulong[count];
                        sw.Start();
                        for (int i = 0; i < count; i++)
                        {
                            heap.Add(i.ToString(), item);
                        }
                        sw.Stop();
                        writeRate = (float)count / (sw.ElapsedMilliseconds / 1000f);
                        tx.Complete();
                    }

                    sw.Reset();
                    sw.Start();
                    for (int i = 0; i < count; i++)
                    {
                        item = heap[i.ToString()];
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
            File.Delete(keyName);

            Assert.Inconclusive("Write Rate: {0}, Read Rate: {1}, Load Rate: {2}, Enumerate Rate: {3}", writeRate, readRate, loadRate, enumerateRate);
        }

        [TestMethod]
        public void CanShareHeap()
        {
            var fileName = "Dictionary.dic";
            var keyName1 = Path.GetFileNameWithoutExtension(fileName) + "_keys1.bin";
            var keyName2 = Path.GetFileNameWithoutExtension(fileName) + "_keys2.bin";
            var keyName3 = Path.GetFileNameWithoutExtension(fileName) + "_keys3.bin";
            var heapName = "Heap.loh";
            File.Delete(fileName);
            File.Delete(keyName1);
            File.Delete(keyName2);
            File.Delete(keyName3);
            File.Delete(heapName);

            float writeRate, readRate;
            var sw = new Stopwatch();

            using (var heap = new PersistentHeap(heapName))
            {
                using (PersistentDictionary<string, CustomItem>
                    dictionary1 = new PersistentDictionary<string, CustomItem>(keyName1, heap),
                    dictionary2 = new PersistentDictionary<string, CustomItem>(keyName2, heap))
                {
                    using (PersistentDictionary<string, int>
                        dictionary3 = new PersistentDictionary<string, int>(keyName3, heap))
                    {
                        var count = 10000f;
                        sw.Start();
                        using (var tx = new TransactionScope())
                        {
                            using (var scope = new FlushScope())
                            {
                                for (int i = 0; i < count; i++)
                                {
                                    dictionary1.Add(i.ToString(), new CustomItem() { A = i, B = "1 some text" });
                                    dictionary2.Add(i.ToString(), new CustomItem() { A = i, B = "1 some text" });
                                    dictionary3.Add(i.ToString(), i);
                                }
                            }
                            tx.Complete();
                        }
                        sw.Stop();
                        writeRate = (3f * count) / (sw.ElapsedMilliseconds / 1000f);
                        sw.Reset();

                        sw.Start();
                        for (int i = 0; i < count; i++)
                        {
                            var d1 = dictionary1[i.ToString()];
                            var d2 = dictionary2[i.ToString()];
                            var d3 = dictionary3[i.ToString()];
                        }
                        sw.Stop();
                        readRate = (3f * count) / (sw.ElapsedMilliseconds / 1000f);
                        sw.Reset();
                    }
                }
            }

            File.Delete(fileName);
            File.Delete(keyName1);
            File.Delete(keyName2);
            File.Delete(keyName3);
            File.Delete(heapName);

            Assert.Inconclusive("Write Rate: {0}, Read Rate: {1}", writeRate, readRate);
        }
    }
}
