using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Protocols
{
    public class SocketOptions
    {
        public const int BUFFER_SIZE = 4096;
        public const int MTU_SIZE = 1300;
        public static IEnumerable<byte[]> Chunk(byte[] data)
        {
            bool read = true;
            MemoryStream ms = new MemoryStream(data);
            while (read)
            {
                byte[] chunk;
                if (ms.Length - ms.Position >= BUFFER_SIZE)
                {
                    chunk = new byte[BUFFER_SIZE];
                }
                else
                {
                    chunk = new byte[ms.Length - ms.Position];
                    read = false;
                }
                ms.Read(chunk, 0, chunk.Length);
                yield return chunk;
            }
        }
    }
}
