using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Collections.IO
{
    public unsafe class BytePointerAdapter
    {
        public BytePointerAdapter(ref byte* ptr, long startOffset, long endOffset)
        {
            UpdatePointer(ref ptr, startOffset, endOffset);
        }

        public void UpdatePointer(ref byte* ptr, long startOffset, long endOffset)
        {
            Pointer = ptr;
            BasePointer = ptr;
            StartOffset = startOffset;
            EndOffset = endOffset;
        }

        public byte* BasePointer;
        private byte* Pointer;

        public long StartOffset { get; private set; }
        public long EndOffset { get; private set; }

        public long ReadInt64(long offset)
        {
            Pointer = BasePointer + offset;
            return *(((long*)Pointer));
        }

        public ulong ReadUInt64(long offset)
        {
            Pointer = BasePointer + offset;
            return *(((ulong*)Pointer));
        }

        public int ReadInt32(long offset)
        {
            Pointer = BasePointer + offset;
            return *(((int*)Pointer));
        }

        public uint ReadUInt32(long offset)
        {
            Pointer = BasePointer + offset;
            return *(((uint*)Pointer));
        }

        public short ReadInt16(long offset)
        {
            Pointer = BasePointer + offset;
            return *(((short*)Pointer));
        }

        public ushort ReadUInt16(long offset)
        {
            Pointer = BasePointer + offset;
            return *(((ushort*)Pointer));
        }

        public float ReadSingle(long offset)
        {
            Pointer = BasePointer + offset;
            return *(((float*)Pointer));
        }

        public double ReadDouble(long offset)
        {
            Pointer = BasePointer + offset;
            return *(((double*)Pointer));
        }

        public byte ReadByte(long offset)
        {
            Pointer = BasePointer + offset;
            return *(((byte*)Pointer));
        }

        public bool ReadBoolean(long offset)
        {
            Pointer = BasePointer + offset;
            return *(((bool*)Pointer));
        }

        public DateTime ReadDateTime(long offset)
        {
            Pointer = BasePointer + offset;
            return DateTime.FromBinary(*(((long*)Pointer)));
        }

        public char ReadChar(long offset)
        {
            Pointer = BasePointer + offset;
            return *(((char*)Pointer));
        }

        public byte[] ReadBytes(int offset, int length)
        {
            byte[] arr = new byte[length];
            byte* ptr = (byte*)0;
            Marshal.Copy(IntPtr.Add((IntPtr)BasePointer, offset), arr, 0, length);
            return arr;
        }

        public decimal ReadDecimal(long offset)
        {
            Pointer = BasePointer + offset;
            return *(((decimal*)Pointer));
        }

        public void Write(long position, byte value)
        {
            Pointer = BasePointer + position;
            *(((byte*)Pointer)) = value;
        }

        public void Write(long position, bool value)
        {
            Pointer = BasePointer + position;
            *(((bool*)Pointer)) = value;
        }

        public void Write(long position, char value)
        {
            Pointer = BasePointer + position;
            *(((char*)Pointer)) = value;
        }

        public void Write(long position, short value)
        {
            Pointer = BasePointer + position;
            *(((short*)Pointer)) = value;
        }

        public void Write(long position, ushort value)
        {
            Pointer = BasePointer + position;
            *(((ushort*)Pointer)) = value;
        }

        public void Write(long position, int value)
        {
            Pointer = BasePointer + position;
            *(((int*)Pointer)) = value;
        }

        public void Write(long position, uint value)
        {
            Pointer = BasePointer + position;
            *(((uint*)Pointer)) = value;
        }

        public void Write(long position, Single value)
        {
            Pointer = BasePointer + position;
            *(((Single*)Pointer)) = value;
        }

        public void Write(long position, long value)
        {
            Pointer = BasePointer + position;
            *(((long*)Pointer)) = value;
        }

        public void Write(long position, ulong value)
        {
            Pointer = BasePointer + position;
            *(((ulong*)Pointer)) = value;
        }

        public void Write(long position, double value)
        {
            Pointer = BasePointer + position;
            *(((double*)Pointer)) = value;
        }

        public void Write(long position, decimal value)
        {
            Pointer = BasePointer + position;
            *(((decimal*)Pointer)) = value;
        }

        public void Write(long position, DateTime value)
        {
            Pointer = BasePointer + position;
            *(((long*)Pointer)) = value.ToBinary();
        }

        public void Write(long position, long[] value, int start, int length)
        {
            for (int i = start; i < start + length; i++)
            {
                Write(position + (i - start) * 8, value[i]);
            }
        }

        public void Write(int position, byte[] value)
        {
            Write(position, value, 0, value.Length);
        }

        public void Write(int position, byte[] value, int start, int length)
        {
            byte* ptr = (byte*)0;
            Marshal.Copy(value, start, IntPtr.Add((IntPtr)BasePointer, position), length);
        }
    }
}
