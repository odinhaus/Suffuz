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
        public void CanSerializeSimpleMemebrs()
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
                Q = "foo",
                nA = true
            };

            var serialized = instance.Serialize(testPoco);
            var poco = instance.Deserialize(serialized);

            Assert.IsTrue(testPoco.A.Equals(poco.A));
            Assert.IsTrue(testPoco.B.Equals(poco.B));
            Assert.IsTrue(testPoco.C.Equals(poco.C));
            Assert.IsTrue(testPoco.D.Equals(poco.D));
            Assert.IsTrue(testPoco.E.Equals(poco.E));
            Assert.IsTrue(testPoco.F.Equals(poco.F));
            Assert.IsTrue(testPoco.G.Equals(poco.G));
            Assert.IsTrue(testPoco.H.Equals(poco.H));
            Assert.IsTrue(testPoco.I.Equals(poco.I));
            Assert.IsTrue(testPoco.J.Equals(poco.J));
            Assert.IsTrue(testPoco.K.Equals(poco.K));
            Assert.IsTrue(testPoco.L.Equals(poco.L));
            Assert.IsTrue(testPoco.M.Equals(poco.M));
            Assert.IsTrue(testPoco.N.Length.Equals(poco.N.Length) && poco.N[0] == 1 && poco.N[1] == 2 && poco.N[2] == 3);
            Assert.IsTrue(testPoco.O.Length.Equals(poco.O.Length) && poco.O[0] == 'F' && poco.O[1] == 'o' && poco.O[2] == 'o');
            Assert.IsTrue(testPoco.P.Equals(poco.P));
            Assert.IsTrue(testPoco.Q.Equals(poco.Q));

            testPoco = new SimplePOCO()
            {
                Q = null,
                N = null,
                O = null
            };

            serialized = instance.Serialize(testPoco);
            poco = instance.Deserialize(serialized);

            Assert.IsTrue(poco.Q == null);
            Assert.IsTrue(poco.N == null);
            Assert.IsTrue(poco.O == null);


#if (DEBUG)
            builder.SaveAssembly();
#endif
        }
    }
}
