using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Altus.Suffūz.Serialization.Binary;

namespace Altus.Suffūz.Collections.Tests
{
    [TestClass]
    public class ListTests
    {
        [TestMethod]
        public void CanCreateNewSimpleListInstance()
        {
            var filePath = "";
            using (var list = new Altus.Suffūz.Collections.List<int>())
            {
                filePath = list.File.Name;
            }
            File.Delete(filePath);
        }

        [TestMethod]
        public void CanOpenNewSimpleListInstance()
        {
            var filePath = "IntList.bin";

            using (var list = new Altus.Suffūz.Collections.List<int>(filePath))
            {
                filePath = list.File.Name;
            }

            using (var list = new Altus.Suffūz.Collections.List<int>(filePath))
            {
                filePath = list.File.Name;
            }

            File.Delete(filePath);
        }

        [TestMethod]
        public void CanCountEmptyList()
        {
            var filePath = "IntList.bin";

            using (var list = new Altus.Suffūz.Collections.List<int>(filePath))
            {
                filePath = list.File.Name;
                var count = list.Count;
                Assert.IsTrue(count == 0);
            }

            using (var list = new Altus.Suffūz.Collections.List<int>(filePath))
            {
                filePath = list.File.Name;
                var count = list.Count;
                Assert.IsTrue(count == 0);
            }

            File.Delete(filePath);
        }

        [TestMethod]
        public void CanAddInt32List()
        {
            var filePath = "IntList.bin";

            using (var list = new Altus.Suffūz.Collections.List<int>(filePath))
            {
                filePath = list.File.Name;
                var count = list.Count;
                Assert.IsTrue(count == 0);
                list.Add(5);
                count = list.Count;
                Assert.IsTrue(count == 1);
            }

            using (var list = new Altus.Suffūz.Collections.List<int>(filePath))
            {
                filePath = list.File.Name;
                var count = list.Count;
                Assert.IsTrue(count == 1);
            }

            File.Delete(filePath);
        }

        [TestMethod]
        public void CanAddCustomItemList()
        {
            var filePath = "IntList.bin";

            using (var list = new Altus.Suffūz.Collections.List<CustomItem>(filePath))
            {
                filePath = list.File.Name;
                var count = list.Count;
                Assert.IsTrue(count == 0);
                list.Add(new CustomItem() { A = 24, B = "Foo" });
                count = list.Count;
                Assert.IsTrue(count == 1);
            }

            using (var list = new Altus.Suffūz.Collections.List<CustomItem>(filePath))
            {
                filePath = list.File.Name;
                var count = list.Count;
                Assert.IsTrue(count == 1);
            }

            File.Delete(filePath);
        }

        [TestMethod]
        public void CanEnumerateIntList()
        {
            var filePath = "IntList.bin";

            using (var list = new Altus.Suffūz.Collections.List<int>(filePath))
            {
                filePath = list.File.Name;
                var count = list.Count;
                Assert.IsTrue(count == 0);
                list.Add(5);
                count = list.Count;
                Assert.IsTrue(count == 1);
            }

            using (var list = new Altus.Suffūz.Collections.List<int>(filePath))
            {
                filePath = list.File.Name;
                var count = list.Count;
                Assert.IsTrue(count == 1);
                var first = list.First();
            }

            File.Delete(filePath);
        }
    }


   

}
