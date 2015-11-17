using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Altus.Suffūz.Serialization.Binary;
using Altus.Suffūz.Serialization;
using System.Diagnostics;

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
                A = true,
                B = 1,
                C = 1,
                D = (char)1,
                E = 1,
                F = 1,
                G = 1,
                H = 1,
                I = 1,
                J = 1,
                K = 1,
                L = 1,
                M = 1,
                N = new byte[] { 1, 2, 3 },
                O = "Foo".ToCharArray(),
                P = DateTime.Now,
                Q = null,
                R = "Not null"
            };

            var serialized = instance.Serialize(testPoco);
            var poco = instance.Deserialize(serialized);

            Assert.IsTrue(testPoco.Equals(poco));
#if (DEBUG)
            builder.SaveAssembly();
#endif
        }
    }
}
