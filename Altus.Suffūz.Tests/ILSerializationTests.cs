using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Altus.Suffūz.Serialization.Binary;
using Altus.Suffūz.Serialization;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Altus.Suffūz.Protocols;
using Altus.Suffūz.Messages;
using Altus.Suffūz.Routing;

namespace Altus.Suffūz.Tests
{
    [TestClass]
    public class ILSerializationTests
    {
        [TestMethod]
        public void CanSerializeSimpleMembers()
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
                R = AnEnum.Fish,
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
            Assert.IsTrue(testPoco.R.Equals(poco.R));

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
        }

        [TestMethod]
        public void CanSerializeNullableMembers()
        {
            var builder = new ILSerializerBuilder();
            var instance = builder.CreateSerializerType<SimplePOCO>();

            Assert.IsTrue(instance.SupportsType(typeof(SimplePOCO)));
            Assert.IsTrue(instance.SupportsType(instance.GetType()));
            Assert.IsTrue(instance.SupportsFormat(StandardFormats.BINARY));
            Assert.IsFalse(instance.SupportsFormat(StandardFormats.CSV));

            var testPoco = new SimplePOCO()
            {
                nA = true,
                nB = 1,
                nC = 1,
                nD = (char)1,
                nE = 1,
                nF = 1,
                nG = 1,
                nH = 1,
                nI = 1,
                nJ = 1,
                nK = 1,
                nL = 1,
                nM = 1,
                nP = DateTime.Now,
            };

            var serialized = instance.Serialize(testPoco);
            var poco = instance.Deserialize(serialized);

            Assert.IsTrue(testPoco.nA.Equals(poco.nA));
            Assert.IsTrue(testPoco.nB.Equals(poco.nB));
            Assert.IsTrue(testPoco.nC.Equals(poco.nC));
            Assert.IsTrue(testPoco.nD.Equals(poco.nD));
            Assert.IsTrue(testPoco.nE.Equals(poco.nE));
            Assert.IsTrue(testPoco.nF.Equals(poco.nF));
            Assert.IsTrue(testPoco.nG.Equals(poco.nG));
            Assert.IsTrue(testPoco.nH.Equals(poco.nH));
            Assert.IsTrue(testPoco.nI.Equals(poco.nI));
            Assert.IsTrue(testPoco.nJ.Equals(poco.nJ));
            Assert.IsTrue(testPoco.nK.Equals(poco.nK));
            Assert.IsTrue(testPoco.nL.Equals(poco.nL));
            Assert.IsTrue(testPoco.nM.Equals(poco.nM));
            Assert.IsTrue(testPoco.nP.Equals(poco.nP));
        }

        [TestMethod]
        public void CanSerializeNullableDateTime()
        {
            var builder = new ILSerializerBuilder();
            var instance = builder.CreateSerializerType<NDateTime>();
            var testPoco = new NDateTime() { A = DateTime.Now };
            var serialized = instance.Serialize(testPoco);
            var poco = instance.Deserialize(serialized);
            Assert.IsTrue(testPoco.A.Equals(poco.A));
        }

        [TestMethod]
        public void CanSerializeValueTypeArrays()
        {
            var builder = new ILSerializerBuilder();
            var instanceInt = builder.CreateSerializerType<Array<int>>();
            var testPocoInt = new Array<int>() { A = new int[] { 3, 2 } };
            var serializedInt = instanceInt.Serialize(testPocoInt);
            var pocoInt = instanceInt.Deserialize(serializedInt);
            Assert.IsTrue(testPocoInt.A.Length.Equals(pocoInt.A.Length) && testPocoInt.A[0] == pocoInt.A[0] && testPocoInt.A[1] == pocoInt.A[1]);

            var instanceDec = builder.CreateSerializerType<Array<decimal>>();
            var testPocoDec = new Array<decimal>() { A = new decimal[] { 3M, 2M, 4M } };
            var serializedDec = instanceDec.Serialize(testPocoDec);
            var pocoDec = instanceDec.Deserialize(serializedDec);
            Assert.IsTrue(testPocoDec.A.Length.Equals(pocoDec.A.Length) && testPocoDec.A[0] == pocoDec.A[0] && testPocoDec.A[1] == pocoDec.A[1]);
        }

        [TestMethod]
        public void CanSerializeStringArrays()
        {
            var builder = new ILSerializerBuilder();
            var instance = builder.CreateSerializerType<Array<string>>();
            var testPoco = new Array<string>() { A = new string[] { "Foo", "Bar" } };
            var serialized = instance.Serialize(testPoco);
            var pocoInt = instance.Deserialize(serialized);
            Assert.IsTrue(testPoco.A.Length.Equals(pocoInt.A.Length));
            Assert.IsTrue(testPoco.A[0].Equals(pocoInt.A[0]));
            Assert.IsTrue(testPoco.A[1].Equals(pocoInt.A[1]));
        }

        [TestMethod]
        public void CanSerializeDateTimeArrays()
        {
            var builder = new ILSerializerBuilder();
            var instance = builder.CreateSerializerType<Array<DateTime>>();
            var testPoco = new Array<DateTime>() { A = new DateTime[] { DateTime.Now, DateTime.Now.AddDays(1) } };
            var serialized = instance.Serialize(testPoco);
            var poco = instance.Deserialize(serialized);
            Assert.IsTrue(testPoco.A.Length.Equals(poco.A.Length));
            Assert.IsTrue(testPoco.A[0].Equals(poco.A[0]));
            Assert.IsTrue(testPoco.A[1].Equals(poco.A[1]));
        }

        [TestMethod]
        public void CanSerializeNullableValueTypeArrays()
        {
            var builder = new ILSerializerBuilder();
            var instance = builder.CreateSerializerType<Array<int?>>();
            var testPoco = new Array<int?>() { A = new int?[] { 1, null, 2 } };
            var serialized = instance.Serialize(testPoco);
            var poco = instance.Deserialize(serialized);
            Assert.IsTrue(testPoco.A.Length.Equals(poco.A.Length));
            Assert.IsTrue(testPoco.A[0].Equals(poco.A[0]));
            Assert.IsTrue(testPoco.A[1].Equals(poco.A[1]));
            Assert.IsTrue(testPoco.A[2].Equals(poco.A[2]));
        }

        [TestMethod]
        public void CanSerializeNullableDateTimeArrays()
        {
            var builder = new ILSerializerBuilder();
            var instance = builder.CreateSerializerType<Array<DateTime?>>();
            var testPoco = new Array<DateTime?>() { A = new DateTime?[] { DateTime.Now, null, DateTime.Now.AddDays(1) } };
            var serialized = instance.Serialize(testPoco);
            var poco = instance.Deserialize(serialized);
            Assert.IsTrue(testPoco.A.Length.Equals(poco.A.Length));
            Assert.IsTrue(testPoco.A[0].Equals(poco.A[0]));
            Assert.IsTrue(testPoco.A[1].Equals(poco.A[1]));
            Assert.IsTrue(testPoco.A[2].Equals(poco.A[2]));
        }

        [TestMethod]
        public void CanSerializeComplexMembers()
        {
            var builder = new ILSerializerBuilder();
            var instance = builder.CreateSerializerType<ComplexPOCO>();
            var testPoco = new ComplexPOCO()
            {
                SimplePOCO = new SimplePOCO() { A = true },
                ListOfInt = new List<int>() { 1, 3, 2 },
                IEnumerableOfSimplePOCO = (new SimplePOCO[] { new SimplePOCO() { B = 5 } }).AsEnumerable(),
                CollectionOfSimplePOCO = new ObservableCollection<SimplePOCO>() { new SimplePOCO() { L = 3.2d } },
            };
            var serialized = instance.Serialize(testPoco);
            var poco = instance.Deserialize(serialized);
            Assert.IsTrue(testPoco.SimplePOCO.A.Equals(poco.SimplePOCO.A));
        }

        [TestMethod]
        public void CanSerializeRoutablePayload()
        {
            var builder = new ILSerializerBuilder();
            var instance = builder.CreateSerializerType<RoutablePayload>();
            var testPoco = new RoutablePayload()
            {
                Payload = new NominateExecutionRequest()
                {
                    Nominator = "Some text here",
                    Request = NoArgs.Empty,
                    ScalarResults = true
                },
                PayloadType = "SomeTypeName",
                ReturnType = "SomeTypeName"
            };
            var serialized = instance.Serialize(testPoco);
            var poco = instance.Deserialize(serialized);
        }

        static bool _beenHere = false;
        [TestInitialize]
        public void Init()
        {
#if (DEBUG)
            if (!_beenHere)
            {
                //var builder = new ILSerializerBuilder();
                //builder.CreateSerializerType<SimplePOCO>();
                //builder.CreateSerializerType<NDateTime>();
                //builder.CreateSerializerType<Array<int>>();
                //builder.CreateSerializerType<Array<double>>();
                //builder.CreateSerializerType<Array<long>>();
                //builder.CreateSerializerType<Array<decimal>>();
                //builder.CreateSerializerType<Array<string>>();
                //builder.CreateSerializerType<Array<DateTime>>();
                //builder.CreateSerializerType<Array<int?>>();
                //builder.CreateSerializerType<Array<decimal?>>();
                //builder.CreateSerializerType<Array<DateTime?>>();
                //builder.CreateSerializerType<ComplexPOCO>();
                //builder.CreateSerializerType<RoutablePayload>();
                //builder.CreateSerializerType<NominateExecutionRequest>();
                //builder.CreateSerializerType<NoArgs>();
                //builder.SaveAssembly();
                _beenHere = true;
            }
#endif
        }
    }
}
