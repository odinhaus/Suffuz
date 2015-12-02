using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
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

    }
}
