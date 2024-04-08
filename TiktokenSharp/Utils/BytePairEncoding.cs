using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TiktokenSharp.Utils
{

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


    }

}
