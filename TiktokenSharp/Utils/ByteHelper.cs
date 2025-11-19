using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TiktokenSharp.Utils
{
    internal class ByteHelper
    {
        public static string ConvertByteListToString(List<ReadOnlyMemory<byte>> byteList)
        {
            // Calculate total length
            int totalLength = 0;
            foreach (var memory in byteList)
            {
                totalLength += memory.Length;
            }

#if NETSTANDARD2_0
            // netstandard2.0 doesn't support Span<byte> in GetString
            byte[] allBytes = new byte[totalLength];
            int currentPos = 0;
            foreach (ReadOnlyMemory<byte> memory in byteList)
            {
                memory.Span.CopyTo(allBytes.AsSpan(currentPos));
                currentPos += memory.Length;
            }
            return Encoding.UTF8.GetString(allBytes);
#else
            // Use stack allocation for small strings, heap for large ones
            if (totalLength <= 512)
            {
                Span<byte> stackBuffer = stackalloc byte[totalLength];
                int currentPos = 0;
                foreach (ReadOnlyMemory<byte> memory in byteList)
                {
                    memory.Span.CopyTo(stackBuffer.Slice(currentPos));
                    currentPos += memory.Length;
                }
                return Encoding.UTF8.GetString(stackBuffer);
            }
            else
            {
                byte[] allBytes = new byte[totalLength];
                int currentPos = 0;
                foreach (ReadOnlyMemory<byte> memory in byteList)
                {
                    memory.Span.CopyTo(allBytes.AsSpan(currentPos));
                    currentPos += memory.Length;
                }
                return Encoding.UTF8.GetString(allBytes);
            }
#endif
        }

        public static ReadOnlySpan<byte> ConvertReadOnlyMemoryCharToByte(ReadOnlyMemory<char> charMemory)
        {
            var charSpan = charMemory.Span;
            var bytes = MemoryMarshal.AsBytes(charSpan);
            return bytes;
        }

    }
}
