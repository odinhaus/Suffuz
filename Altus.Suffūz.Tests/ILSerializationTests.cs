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
        public void CanCompileSerializerForSimplePOCO()
        {
            var builder = new ILSerializerBuilder();
            var instance = builder.CreateSerializerType<SimplePOCO>();

            Assert.IsTrue(instance.SupportsType(typeof(SimplePOCO)));
            Assert.IsTrue(instance.SupportsType(instance.GetType()));
            Assert.IsTrue(instance.SupportsFormat(StandardFormats.BINARY));
            Assert.IsFalse(instance.SupportsFormat(StandardFormats.CSV));

            builder.SaveAssembly();
        }
    }

    public class SimplePOCO
    {
        public int Prop1 { get; set; }
    }
}
