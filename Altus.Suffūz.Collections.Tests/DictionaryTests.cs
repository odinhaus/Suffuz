using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                Assert.IsTrue(dictionary.MaximumSize == 1024 * 64);
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
            var count = 1000000;
            var sw = new Stopwatch();
            var item = new CustomItem() { A = 12, B = "some text here" };
            using (var scope = new FlushScope())
            {
                using (var heap = new PersistentDictionary<string, CustomItem>(fileName, 1024 * 1024 * 100))
                {
                    var addresses = new ulong[count];
                    sw.Start();
                    for (int i = 0; i < count; i++)
                    {
                        heap.Add(i.ToString(), item);
                    }
                    sw.Stop();
                    writeRate = (float)count / (sw.ElapsedMilliseconds / 1000f);

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
    }
}
