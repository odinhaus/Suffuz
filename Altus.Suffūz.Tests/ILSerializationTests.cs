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
            };
            var serialized = instance.Serialize(testPoco);
            var poco = instance.Deserialize(serialized);

            Assert.IsTrue(testPoco.Equals(poco));

            builder.SaveAssembly();
        }
    }

    public class SimplePOCO
    {
        [BinarySerializable(0)]
        public bool A { get; set; }

        [BinarySerializable(1)]
        public byte B { get; set; }

        [BinarySerializable(2)]
        public sbyte C { get; set; }

        [BinarySerializable(3)]
        public char D { get; set; }

        [BinarySerializable(4)]
        public short E { get; set; }

        [BinarySerializable(5)]
        public ushort F { get; set; }

        [BinarySerializable(6)]
        public int G { get; set; }

        [BinarySerializable(7)]
        public uint H { get; set; }

        [BinarySerializable(8)]
        public long I { get; set; }

        [BinarySerializable(9)]
        public ulong J { get; set; }

        [BinarySerializable(10)]
        public float K { get; set; }

        [BinarySerializable(11)]
        public double L { get; set; }

        [BinarySerializable(12)]
        public decimal M { get; set; }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as SimplePOCO);
        }

        public bool Equals(SimplePOCO value)
        {
            return value != null
                && value.A == A
                && value.B == B
                && value.C == C
                && value.D == D
                && value.E == E
                && value.F == F
                && value.G == G
                && value.H == H
                && value.I == I
                && value.J == J
                && value.K == K
                && value.L == L
                && value.M == M;
        }
    }
}
