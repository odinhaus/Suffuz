using Altus.Suffusion.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion.Serialization.Binary
{
    public class PrimitiveSerializer : ISerializer
    {
        public int Priority { get; private set; }
        public bool IsScalar { get { return true; } }
        public static bool IsPrimitive(Type t)
        {
            return t == typeof(bool)
                || t == typeof(byte)
                || t == typeof(char)
                || t == typeof(ushort)
                || t == typeof(short)
                || t == typeof(uint)
                || t == typeof(int)
                || t == typeof(ulong)
                || t == typeof(long)
                || t == typeof(float)
                || t == typeof(double)
                || t == typeof(DateTime)
                || t == typeof(byte[])
                || t == typeof(Decimal);
        }

        public byte[] Serialize(object source)
        {
            Type t = source.GetType();
            if (t == typeof(byte))
                return BitConverter.GetBytes((byte)(object)source);
            if (t == typeof(char))
                return BitConverter.GetBytes((char)(object)source);
            if (t == typeof(ushort))
                return BitConverter.GetBytes((ushort)(object)source);
            if (t == typeof(short))
                return BitConverter.GetBytes((short)(object)source);
            if (t == typeof(uint))
                return BitConverter.GetBytes((uint)(object)source);
            if (t == typeof(int))
                return BitConverter.GetBytes((int)(object)source);
            if (t == typeof(ulong))
                return BitConverter.GetBytes((ulong)(object)source);
            if (t == typeof(decimal))
                return GetBytes(Decimal.GetBits((decimal)(object)source));
            if (t == typeof(long))
                return BitConverter.GetBytes((long)(object)source);
            if (t == typeof(float))
                return BitConverter.GetBytes((float)(object)source);
            if (t == typeof(double))
                return BitConverter.GetBytes((double)(object)source);
            if (t == typeof(bool))
                return BitConverter.GetBytes((bool)(object)source);
            if (t == typeof(DateTime))
                return BitConverter.GetBytes(((DateTime)(object)source).ToBinary());
            if (t == typeof(byte[]))
                return (byte[])source;


            throw (new InvalidCastException("The provided type is not a supported primitive type."));
        }

        public byte[] GetBytes(int[] ints)
        {
            var byteList = new List<Byte>();
            foreach(var i in ints)
            {
                byteList.AddRange(BitConverter.GetBytes(i));
            }
            return byteList.ToArray();
        }

        public object Deserialize(byte[] source, Type targetType)
        {
            if (targetType == typeof(bool))
                return BitConverter.ToBoolean(source, 0);
            if (targetType == typeof(byte))
                return source[0];
            if (targetType == typeof(char))
                return BitConverter.ToChar(source, 0);
            if (targetType == typeof(ushort))
                return BitConverter.ToUInt16(source, 0);
            if (targetType == typeof(short))
                return BitConverter.ToInt16(source, 0);
            if (targetType == typeof(uint))
                return BitConverter.ToUInt32(source, 0);
            if (targetType == typeof(int))
                return BitConverter.ToInt32(source, 0);
            if (targetType == typeof(ulong))
                return BitConverter.ToUInt64(source, 0);
            if (targetType == typeof(long))
                return BitConverter.ToInt64(source, 0);
            if (targetType == typeof(float))
                return BitConverter.ToSingle(source, 0);
            if (targetType == typeof(double))
                return BitConverter.ToDouble(source, 0);
            if (targetType == typeof(decimal))
                return new decimal(ToInts(source, 0));
            if (targetType == typeof(DateTime))
                return DateTime.FromBinary(BitConverter.ToInt64(source, 0));
            if (targetType == typeof(string))
                return SerializationContext.Instance.TextEncoding.GetString(source);
            if (targetType == typeof(byte[]))
                return source;

            throw (new InvalidCastException("The provided type is not a supported primitive type."));
        }

        public int[] ToInts(byte[] bytes, int startIndex)
        {
            var ints = new int[4];
            for(int i = 0; i < 16; i++)
            {
                ints[i / 4] = BitConverter.ToInt32(bytes, i + startIndex);
            }
            return ints;
        }

        public bool SupportsFormat(string format)
        {
            return format.Equals(StandardFormats.BINARY, StringComparison.InvariantCultureIgnoreCase);
        }

        public virtual bool SupportsType(Type type)
        {
            return PrimitiveSerializer.IsPrimitive(type);
        }

        public static byte GetByteCount(Type targetType)
        {
            if (targetType == typeof(bool)
                || targetType == typeof(byte))
                return (byte)1;

            if (targetType == typeof(char)
                || targetType == typeof(ushort)
                || targetType == typeof(short))
                return (byte)2;

            if (targetType == typeof(uint)
                || targetType == typeof(int)
                || targetType == typeof(float))
                return (byte)4;

            if (targetType == typeof(ulong)
                || targetType == typeof(long)
                || targetType == typeof(double)
                || targetType == typeof(DateTime))
                return 8;

            if (targetType == typeof(decimal))
                return 16;

            return 0;
        }
    }


    public class PrimitiveSerializer<T> : PrimitiveSerializer, ISerializer<T>
    {
        public static bool IsPrimitive()
        {
            return typeof(T) == typeof(bool)
                || typeof(T) == typeof(byte)
                || typeof(T) == typeof(char)
                || typeof(T) == typeof(ushort)
                || typeof(T) == typeof(short)
                || typeof(T) == typeof(uint)
                || typeof(T) == typeof(int)
                || typeof(T) == typeof(ulong)
                || typeof(T) == typeof(long)
                || typeof(T) == typeof(float)
                || typeof(T) == typeof(double)
                || typeof(T) == typeof(DateTime)
                || typeof(T) == typeof(byte[])
                || typeof(T) == typeof(decimal[]);
        }

        public byte[] Serialize(T source)
        {
            return base.Serialize(source);
        }

        public void Serialize(T source, Stream outputStream)
        {
            StreamHelper.Copy(Serialize(source), outputStream);
        }

        public T Deserialize(byte[] source)
        {
            return (T)base.Deserialize(source, typeof(T));
        }

        public T Deserialize(Stream inputSource)
        {
            return Deserialize(inputSource.GetBytes(GetByteCount(typeof(T))));
        }

        public override bool SupportsType(Type type)
        {
            return type == typeof(T);
        }
    }
}
