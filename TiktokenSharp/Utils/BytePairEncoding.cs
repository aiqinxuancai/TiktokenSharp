using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace TiktokenSharp.Utils
{

    public class BytePairEncoding
    {

#if NETSTANDARD2_1_OR_GREATER || NET7_0_OR_GREATER

        private static int? GetRank(Span<byte> piece, int startIdx, int skip, Dictionary<byte[], int> ranks)
        {
            if (startIdx + skip + 2 < piece.Length)
            {
                var slice = piece.Slice(startIdx, piece.Length - startIdx - skip - 2).ToArray();
                if (ranks.TryGetValue(slice, out var rank))
                {
                    return rank;
                }
            }
            return null;
        }

        private static void UpdateParts(List<(int, int)> parts, Span<byte> piece, Dictionary<byte[], int> ranks)
        {
            for (int i = 0; i < parts.Count - 2; i++)
            {
                var rank = GetRank(piece, parts[i].Item1, 0, ranks);
                if (rank != null)
                {
                    Debug.Assert(rank.Value != int.MaxValue);
                    parts[i] = (parts[i].Item1, rank.Value);
                }
            }
        }

        private static List<T> BytePairMerge<T>(Span<byte> piece, Dictionary<byte[], int> ranks, Func<Range, T> f)
        {
            var parts = Enumerable.Range(0, piece.Length + 1).Select(i => (i, int.MaxValue)).ToList();
            UpdateParts(parts, piece, ranks);

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
                    parts[i] = (parts[i].Item1, GetRank(piece, parts[i].Item1, 1, ranks) ?? int.MaxValue);
                    if (i > 0)
                    {
                        parts[i - 1] = (parts[i - 1].Item1, GetRank(piece, parts[i - 1].Item1, 1, ranks) ?? int.MaxValue);
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
#else
        static List<T> BytePairMerge<T>(byte[] piece, Dictionary<byte[], int> ranks, Func<Range, T> f)
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







        public static List<int> BytePairEncode(byte[] piece, Dictionary<byte[], int> ranks)
        {
            if (piece.Length == 1)
            {
                return new List<int> { ranks[piece] };
            }
#if NETSTANDARD2_1_OR_GREATER || NET7_0_OR_GREATER
            return BytePairMerge(piece.AsSpan(), ranks, p => ranks[piece[p.Start..p.End]]);
            //return BytePairMerge(piece, ranks, p => ranks[piece[p.Start..p.End]]);
#else
            return BytePairMerge(piece, ranks, p => ranks[piece[p.Start..p.End]]);
#endif

        }

        public static List<byte[]> BytePairSplit(byte[] piece, Dictionary<byte[], int> ranks)
        {
            if (piece.Length == 1)
            {
                return new List<byte[]> { piece };
            }
#if NETSTANDARD2_1_OR_GREATER || NET7_0_OR_GREATER
            return BytePairMerge(piece.AsSpan(), ranks, p => piece[p.Start..p.End].ToArray());
            //return BytePairMerge(piece, ranks, p => piece[p.Start..p.End]);
#else
            return BytePairMerge(piece, ranks, p => piece[p.Start..p.End]);
#endif


        }

    }

}
