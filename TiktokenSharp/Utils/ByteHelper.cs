using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TiktokenSharp.Utils
{
    internal class ByteHelper
    {
        public static string ConvertByteListToString(List<ReadOnlyMemory<byte>> byteList)
        {
            int totalLength = byteList.Sum(memory => memory.Length);
            byte[] allBytes = new byte[totalLength];
            int currentPos = 0;
            foreach (ReadOnlyMemory<byte> memory in byteList)
            {
                memory.Span.CopyTo(allBytes.AsSpan(currentPos));
                currentPos += memory.Length;
            }
            return Encoding.UTF8.GetString(allBytes);
        }
    }
}
