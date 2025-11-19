using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TiktokenSharp.Utils
{
    // Pool for reusing List<(int, int)> to avoid allocations in BytePairEncode
    internal static class PartsListPool
    {
        [ThreadStatic]
        private static List<(int Start, int Rank)> t_cachedList;

        public static List<(int Start, int Rank)> Rent(int capacity)
        {
            var list = t_cachedList;
            if (list != null)
            {
                t_cachedList = null;
                list.Clear();
                if (list.Capacity < capacity)
                {
                    list.Capacity = capacity;
                }
                return list;
            }
            return new List<(int Start, int Rank)>(capacity);
        }

        public static void Return(List<(int Start, int Rank)> list)
        {
            if (list != null && list.Capacity <= 256) // Don't cache very large lists
            {
                t_cachedList = list;
            }
        }
    }

    // Pool for reusing List<ReadOnlyMemory<byte>> in DecodeNative
    internal static class ByteMemoryListPool
    {
        [ThreadStatic]
        private static List<ReadOnlyMemory<byte>> t_cachedList;

        public static List<ReadOnlyMemory<byte>> Rent(int capacity)
        {
            var list = t_cachedList;
            if (list != null)
            {
                t_cachedList = null;
                list.Clear();
                if (list.Capacity < capacity)
                {
                    list.Capacity = capacity;
                }
                return list;
            }
            return new List<ReadOnlyMemory<byte>>(capacity);
        }

        public static void Return(List<ReadOnlyMemory<byte>> list)
        {
            if (list != null && list.Capacity <= 512) // Don't cache very large lists
            {
                t_cachedList = list;
            }
        }
    }

    internal class BytePairEncoding
    {
#if NET7_0_OR_GREATER
        static List<T> BytePairMerge<T>(ReadOnlyMemory<byte> piece, Dictionary<ReadOnlyMemory<byte>, int> ranks, Func<Range, T> f)
        {
            var parts = new List<(int Start, int Rank)>(piece.Length + 1);

            for (int i = 0; i <= piece.Length; i++)
            {
                parts.Add((i, int.MaxValue));
            }

            int? GetRank(int startIdx, int skip = 0)
            {
                if (startIdx + skip + 2 < parts.Count)
                {
                    ReadOnlyMemory<byte> sliceMemory = piece.Slice(parts[startIdx].Item1, parts[startIdx + skip + 2].Item1 - parts[startIdx].Item1);
                    if (ranks.TryGetValue(sliceMemory, out var rank))
                    {
                        return rank;
                    }
                }
                return null;
            }

            for (int i = 0; i < parts.Count - 2; i++)
            {
                var rank = GetRank(i);
                if (rank != null)
                {
                    parts[i] = (parts[i].Start, rank.Value);
                }
            }

            while (parts.Count > 1)
            {
                var minRank = (Rank: int.MaxValue, Index: 0);
                for (int i = 0; i < parts.Count - 1; i++)
                {
                    if (parts[i].Rank < minRank.Rank)
                    {
                        minRank = (parts[i].Rank, i);
                    }
                }
                if (minRank.Rank != int.MaxValue)
                {
                    int i = minRank.Index;
                    parts[i] = (parts[i].Start, GetRank(i, 1) ?? int.MaxValue);
                    if (i > 0)
                    {
                        parts[i - 1] = (parts[i - 1].Start, GetRank(i - 1, 1) ?? int.MaxValue);
                    }
                    parts.RemoveAt(i + 1);
                }
                else
                {
                    break;
                }
            }

            var outList = new List<T>(parts.Count - 1);
            for (int i = 0; i < parts.Count - 1; i++)
            {
                outList.Add(f(parts[i].Start..parts[i + 1].Start));
            }
            return outList;
        }


#else
        static List<T> BytePairMerge<T>(ReadOnlyMemory<byte> piece, Dictionary<ReadOnlyMemory<byte>, int> ranks, Func<Range, T> f)
        {
            var parts = Enumerable.Range(0, piece.Length + 1).Select(i => (i, int.MaxValue)).ToList();
            int? GetRank(int startIdx, int skip = 0)
            {
                if (startIdx + skip + 2 < parts.Count)
                {
                    var slice = piece[parts[startIdx].Item1..parts[startIdx + skip + 2].Item1];
                    if (ranks.TryGetValue(slice, out var rank))
                    {
                        return rank;
                    }
                }
                return null;
            }
            for (int i = 0; i < parts.Count - 2; i++)
            {
                var rank = GetRank(i);
                if (rank != null)
                {
                    Debug.Assert(rank.Value != int.MaxValue);
                    parts[i] = (parts[i].Item1, rank.Value);
                }
            }
            while (parts.Count > 1)
            {
                var minRank = (int.MaxValue, 0);
                for (int i = 0; i < parts.Count - 1; i++)
                {
                    if (parts[i].Item2 < minRank.Item1)
                    {
                        minRank = (parts[i].Item2, i);
                    }
                }
                if (minRank.Item1 != int.MaxValue)
                {
                    int i = minRank.Item2;
                    parts[i] = (parts[i].Item1, GetRank(i, 1) ?? int.MaxValue);
                    if (i > 0)
                    {
                        parts[i - 1] = (parts[i - 1].Item1, GetRank(i - 1, 1) ?? int.MaxValue);
                    }
                    parts.RemoveAt(i + 1);
                }
                else
                {
                    break;
                }
            }
            var outList = new List<T>(parts.Count - 1);
            for (int i = 0; i < parts.Count - 1; i++)
            {
                outList.Add(f(parts[i].Item1..parts[i + 1].Item1));
            }
            return outList;
        }
#endif


        public static List<int> BytePairEncode(byte[] piece, Dictionary<ReadOnlyMemory<byte>, int> ranks)
        {
            ReadOnlyMemory<byte> pieceMemory = piece;

            if (piece.Length == 1)
            {
                return new List<int> { ranks[pieceMemory] };
            }
            return BytePairMerge(pieceMemory, ranks, range => ranks[pieceMemory.Slice(range.Start.Value, range.End.Value - range.Start.Value)]);
        }

        public static List<byte[]> BytePairSplit(byte[] piece, Dictionary<ReadOnlyMemory<byte>, int> ranks)
        {
            ReadOnlyMemory<byte> pieceMemory = piece;

            if (piece.Length == 1)
            {
                return new List<byte[]> { pieceMemory.ToArray() };
            }
            return BytePairMerge(pieceMemory, ranks, range => pieceMemory.Slice(range.Start.Value, range.End.Value - range.Start.Value).ToArray());
        }

        public static int BytePairEncodeCount(byte[] piece, Dictionary<ReadOnlyMemory<byte>, int> ranks)
        {
            ReadOnlyMemory<byte> pieceMemory = piece;

            if (piece.Length == 1)
            {
                return 1;
            }

            var parts = new List<(int Start, int Rank)>(piece.Length + 1);

            for (int i = 0; i <= piece.Length; i++)
            {
                parts.Add((i, int.MaxValue));
            }

            int? GetRank(int startIdx, int skip = 0)
            {
                if (startIdx + skip + 2 < parts.Count)
                {
                    ReadOnlyMemory<byte> sliceMemory = pieceMemory.Slice(parts[startIdx].Item1, parts[startIdx + skip + 2].Item1 - parts[startIdx].Item1);
                    if (ranks.TryGetValue(sliceMemory, out var rank))
                    {
                        return rank;
                    }
                }
                return null;
            }

            for (int i = 0; i < parts.Count - 2; i++)
            {
                var rank = GetRank(i);
                if (rank != null)
                {
                    parts[i] = (parts[i].Start, rank.Value);
                }
            }

            while (parts.Count > 1)
            {
                var minRank = (Rank: int.MaxValue, Index: 0);
                for (int i = 0; i < parts.Count - 1; i++)
                {
                    if (parts[i].Rank < minRank.Rank)
                    {
                        minRank = (parts[i].Rank, i);
                    }
                }
                if (minRank.Rank != int.MaxValue)
                {
                    int i = minRank.Index;
                    parts[i] = (parts[i].Start, GetRank(i, 1) ?? int.MaxValue);
                    if (i > 0)
                    {
                        parts[i - 1] = (parts[i - 1].Start, GetRank(i - 1, 1) ?? int.MaxValue);
                    }
                    parts.RemoveAt(i + 1);
                }
                else
                {
                    break;
                }
            }

            return parts.Count - 1;
        }

        /// <summary>
        /// Encodes bytes into tokens and adds them directly to the output list to avoid allocation
        /// </summary>
        public static void BytePairEncodeInto(ReadOnlyMemory<byte> pieceMemory, Dictionary<ReadOnlyMemory<byte>, int> ranks, List<int> output)
        {
            if (pieceMemory.Length == 1)
            {
                output.Add(ranks[pieceMemory]);
                return;
            }

            // Rent from pool instead of allocating
            var parts = PartsListPool.Rent(pieceMemory.Length + 1);

            try
            {
                for (int i = 0; i <= pieceMemory.Length; i++)
                {
                    parts.Add((i, int.MaxValue));
                }

                int? GetRank(int startIdx, int skip = 0)
                {
                    if (startIdx + skip + 2 < parts.Count)
                    {
                        ReadOnlyMemory<byte> sliceMemory = pieceMemory.Slice(parts[startIdx].Item1, parts[startIdx + skip + 2].Item1 - parts[startIdx].Item1);
                        if (ranks.TryGetValue(sliceMemory, out var rank))
                        {
                            return rank;
                        }
                    }
                    return null;
                }

                for (int i = 0; i < parts.Count - 2; i++)
                {
                    var rank = GetRank(i);
                    if (rank != null)
                    {
                        parts[i] = (parts[i].Start, rank.Value);
                    }
                }

                while (parts.Count > 1)
                {
                    var minRank = (Rank: int.MaxValue, Index: 0);
                    for (int i = 0; i < parts.Count - 1; i++)
                    {
                        if (parts[i].Rank < minRank.Rank)
                        {
                            minRank = (parts[i].Rank, i);
                        }
                    }
                    if (minRank.Rank != int.MaxValue)
                    {
                        int i = minRank.Index;
                        parts[i] = (parts[i].Start, GetRank(i, 1) ?? int.MaxValue);
                        if (i > 0)
                        {
                            parts[i - 1] = (parts[i - 1].Start, GetRank(i - 1, 1) ?? int.MaxValue);
                        }
                        parts.RemoveAt(i + 1);
                    }
                    else
                    {
                        break;
                    }
                }

                // Add tokens directly to output list
                for (int i = 0; i < parts.Count - 1; i++)
                {
                    var slice = pieceMemory.Slice(parts[i].Start, parts[i + 1].Start - parts[i].Start);
                    output.Add(ranks[slice]);
                }
            }
            finally
            {
                // Return to pool for reuse
                PartsListPool.Return(parts);
            }
        }

    }

}
