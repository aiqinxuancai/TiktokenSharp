using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TiktokenSharp.Utils
{


    internal class ReadOnlyMemoryComparer : IEqualityComparer<ReadOnlyMemory<byte>>
    {
        public bool Equals(ReadOnlyMemory<byte> x, ReadOnlyMemory<byte> y)
        {
            return x.Span.SequenceEqual(y.Span);
        }

        public int GetHashCode(ReadOnlyMemory<byte> obj)
        {
            ReadOnlySpan<byte> span = obj.Span;
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < span.Length; i++)
                {
                    hash = hash * 31 + span[i];
                }
                return hash;
            }
        }
    }


    internal class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] x, byte[] y)
        {
            if (x == null || y == null)
            {
                return x == y;
            }
            if (x.Length != y.Length)
            {
                return false;
            }
            for (int i = 0; i < x.Length; i++)
            {
                if (x[i] != y[i])
                {
                    return false;
                }
            }
            return true;
        }

        public int GetHashCode(byte[] obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            unchecked 
            {
                int hash = 17;
                foreach (byte element in obj)
                {
                    hash = hash * 31 + element; 
                }
                return hash;
            }
        }
    }

}
