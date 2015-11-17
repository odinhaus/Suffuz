using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Tests
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

        [BinarySerializable(16)]
        public string Q { get; set; }


        [BinarySerializable(1000)]
        public bool? nA { get; set; }

        [BinarySerializable(1001)]
        public byte? nB { get; set; }

        [BinarySerializable(1002)]
        public sbyte? nC { get; set; }

        [BinarySerializable(1003)]
        public char? nD { get; set; }

        [BinarySerializable(1004)]
        public short? nE { get; set; }

        [BinarySerializable(1005)]
        public ushort? nF { get; set; }

        [BinarySerializable(1006)]
        public int? nG { get; set; }

        [BinarySerializable(1007)]
        public uint? nH { get; set; }

        [BinarySerializable(1008)]
        public long? nI { get; set; }

        [BinarySerializable(1009)]
        public ulong? nJ { get; set; }

        [BinarySerializable(10010)]
        public float? nK { get; set; }

        [BinarySerializable(10011)]
        public double? nL { get; set; }

        [BinarySerializable(10012)]
        public decimal? nM { get; set; }

        [BinarySerializable(10015)]
        public DateTime? nP { get; set; }

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

    public class NDateTime
    {
        [BinarySerializable(0)]
        public DateTime? A { get; set; }
    }
}
