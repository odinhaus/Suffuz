using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Test
{
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

        [BinarySerializable(13)]
        public byte[] N { get; set; }

        [BinarySerializable(14)]
        public char[] O { get; set; }

        [BinarySerializable(15)]
        public DateTime P { get; set; }

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
                && value.M == M
                && ArraysEqual(value.N, N)
                && ArraysEqual(value.O, O);
        }

        public bool ArraysEqual<T>(T[] a, T[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (!a[i].Equals(b[i])) return false;
            }
            return true;
        }
    }
}
