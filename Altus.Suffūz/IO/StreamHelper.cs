using Altus.Suffūz.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.IO
{
    //========================================================================================================//
    /// <summary>
    /// Class name:  StreamHelper
    /// Class description:
    /// Usage:
    /// <example></example>
    /// <remarks></remarks>
    /// </summary>
    //========================================================================================================//
    public static class StreamHelper
    {


        #region Methods
        #region Public
        public static byte[] GetBytes(this Stream source, int length)
        {
            length = (int)Math.Min(length, source.Length);
            byte[] bytes = new byte[length];
            source.Read(bytes, 0, length);
            return bytes;
        }

        public static byte[] GetBytes(this Stream source)
        {
            var bytes = new List<byte>();
            var buffer = new byte[4096];
            var read = 0;
            do
            {
                read = source.Read(buffer, 0, buffer.Length);
                bytes.AddRange(buffer.Take(read));
            } while (read > 0);
            return bytes.ToArray();
        }

        public static IComparable ToValue(this Stream source, Type T)
        {
            byte[] buffer = new byte[8];
            if (T == typeof(byte))
            {
                source.Read(buffer, 0, 1);
                return buffer[0];
            }
            else if (T == typeof(bool))
            {
                source.Read(buffer, 0, 1);
                return buffer.ToBoolean();
            }
            else if (T == typeof(char))
            {
                source.Read(buffer, 0, 2);
                return buffer.ToChar();
            }
            else if (T == typeof(ushort))
            {
                source.Read(buffer, 0, 2);
                return buffer.ToUInt16();
            }
            else if (T == typeof(short))
            {
                source.Read(buffer, 0, 2);
                return buffer.ToInt16();
            }
            else if (T == typeof(uint))
            {
                source.Read(buffer, 0, 4);
                return buffer.ToUInt32();
            }
            else if (T == typeof(int))
            {
                source.Read(buffer, 0, 4);
                return buffer.ToInt32();
            }
            else if (T == typeof(float))
            {
                source.Read(buffer, 0, 4);
                return buffer.ToSingle();
            }
            else if (T == typeof(ulong))
            {
                source.Read(buffer, 0, 8);
                return buffer.ToUInt64();
            }
            else if (T == typeof(long))
            {
                source.Read(buffer, 0, 8);
                return buffer.ToInt64();
            }
            else if (T == typeof(double))
            {
                source.Read(buffer, 0, 8);
                return buffer.ToDouble();
            }
            else if (T == typeof(decimal))
            {
                source.Read(buffer, 0, 8);
                return buffer.ToDecimal();
            }
            else if (T == typeof(string))
            {
                source.Read(buffer, 0, 4);
                int len = buffer.ToInt32();
                byte[] text = new byte[len];
                source.Read(text, 0, len);
                return Encoding.Unicode.GetString(text);
            }
            else
            {
                throw (new InvalidCastException("Data type not supported"));
            }
        }

        public static void Write(this Stream source, string value)
        {
            byte[] data = App.Resolve<ISerializationContext>().TextEncoding.GetBytes(value);
            source.Write(data, 0, data.Length);
        }

        public static void Write(this Stream source, byte value)
        {
            source.WriteByte(value);
        }

        public static void Write(this Stream source, char value)
        {
            byte[] data = BitConverter.GetBytes(value);
            source.Write(data, 0, data.Length);
        }

        public static void Write(this Stream source, ushort value)
        {
            byte[] data = BitConverter.GetBytes(value);
            source.Write(data, 0, data.Length);
        }

        public static void Write(this Stream source, short value)
        {
            byte[] data = BitConverter.GetBytes(value);
            source.Write(data, 0, data.Length);
        }

        public static void Write(this Stream source, uint value)
        {
            byte[] data = BitConverter.GetBytes(value);
            source.Write(data, 0, data.Length);
        }

        public static void Write(this Stream source, int value)
        {
            byte[] data = BitConverter.GetBytes(value);
            source.Write(data, 0, data.Length);
        }

        public static void Write(this Stream source, ulong value)
        {
            byte[] data = BitConverter.GetBytes(value);
            source.Write(data, 0, data.Length);
        }

        public static void Write(this Stream source, long value)
        {
            byte[] data = BitConverter.GetBytes(value);
            source.Write(data, 0, data.Length);
        }

        public static void Write(this Stream source, float value)
        {
            byte[] data = BitConverter.GetBytes(value);
            source.Write(data, 0, data.Length);
        }

        public static void Write(this Stream source, double value)
        {
            byte[] data = BitConverter.GetBytes(value);
            source.Write(data, 0, data.Length);
        }

        public static void Write(this Stream source, byte[] data)
        {
            source.Write(data, 0, data.Length);
        }

        //========================================================================================================//
        /// <summary>
        /// Returns a new MemoryStream containing a copy of the data in source 
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static MemoryStream Copy(Stream source)
        {
            return new MemoryStream(GetBytes(source));
        }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Copies data from source to destination
        /// </summary>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        public static void Copy(Stream source, Stream destination)
        {
            byte[] bytes = new byte[2048];
            while (true)
            {
                int read = source.Read(bytes, 0, bytes.Length);
                if (read == 0)
                    break;
                destination.Write(bytes, 0, read);
            }
        }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Copies data from the source stream to the destination stream, from start
        /// for count number of total bytes.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="start"></param>
        /// <param name="count"></param>
        /// <param name="destination"></param>
        public static void Copy(Stream source, long start, long count, Stream destination)
        {
            long curPos = source.Position;
            source.Position = start;

            byte[] bytes = new byte[2048];
            long readCount = Math.Min(count, 2048);
            long totalRead = 0;
            while (true)
            {

                int read = source.Read(bytes, 0, (int)readCount);
                totalRead += read;

                if (read == 0)
                    break;

                destination.Write(bytes, 0, read);

                if (totalRead >= count)
                    break;

                readCount = Math.Min(count - totalRead, 2048);

            }
        }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Copies the given byte buffer to the provided destination stream
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="destination"></param>
        public static void Copy(byte[] buffer, Stream destination)
        {
            destination.Write(buffer, 0, buffer.Length);
        }
        //========================================================================================================//

        #endregion Public

        #region Private
        #endregion Private

        #region Protected
        #endregion Protected
        #endregion Methods

    }
}
