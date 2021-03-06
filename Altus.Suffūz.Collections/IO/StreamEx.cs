﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Collections.IO
{
    public unsafe static class StreamEx
    {
        public static void Write(this Stream stream, int value)
        {
            byte[] data = new byte[4];
            fixed (byte* Pointer = data)
            {
                *(((int*)Pointer)) = value;
            }
            stream.Write(data, 0, data.Length);
        }

        public static byte[] ReadBytes(this Stream stream, int length)
        {
            var bytes = new byte[length];
            stream.Read(bytes, 0, length);
            return bytes;
        }

        public static int ReadInt32(this Stream stream)
        {
            fixed (byte* ptr = ReadBytes(stream, 4))
               return *(((int*)ptr));
        }

        public static ulong ReadUInt64(this Stream stream)
        {
            fixed (byte* ptr = ReadBytes(stream, 8))
               return *(((ulong*)ptr));
        }
    }
}
