using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Altus.Suffūz.Serialization.Binary;
using Altus.Suffūz.Serialization;

namespace Altus.Suffūz.Tests
{
    [TestClass]
    public class ILSerializationTests
    {
        [TestMethod]
        public void CanCreateSimplePOCOSerializer()
        {
            var builder = new ILSerializerBuilder();
            var instance = builder.CreateSerializerType<SimplePOCO>();

            Assert.IsTrue(instance.SupportsType(typeof(SimplePOCO)));
            Assert.IsTrue(instance.SupportsType(instance.GetType()));
            Assert.IsTrue(instance.SupportsFormat(StandardFormats.BINARY));
            Assert.IsFalse(instance.SupportsFormat(StandardFormats.CSV));

            var testPoco = new SimplePOCO()
            {
                Prop1 = 3
            };
            var poco = instance.Deserialize(instance.Serialize(testPoco));

            Assert.IsTrue(testPoco.Equals(poco));

            builder.SaveAssembly();
        }

        [TestMethod]
        public void CanCreateSimpleGenericPOCOSerializer()
        {
            var builder = new ILSerializerBuilder();
            var instance = builder.CreateSerializerType<GenericPOCO<int>>();

            Assert.IsTrue(instance.SupportsType(typeof(GenericPOCO<int>)));
            Assert.IsTrue(instance.SupportsType(instance.GetType()));
            Assert.IsTrue(instance.SupportsFormat(StandardFormats.BINARY));
            Assert.IsFalse(instance.SupportsFormat(StandardFormats.CSV));

            var testPoco = new GenericPOCO<int>()
            {
                Prop1 = 3
            };
            var poco = instance.Deserialize(instance.Serialize(testPoco));

            Assert.IsTrue(testPoco.Equals(poco));

            builder.SaveAssembly();
        }
    }

    public class SimplePOCO
    {
        public int Prop1 { get; set; }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }
    }

    public class GenericPOCO<T>
    {
        public T Prop1 { get; set; }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }
    }
}
