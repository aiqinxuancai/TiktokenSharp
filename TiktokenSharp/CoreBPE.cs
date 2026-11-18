using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TiktokenSharp.Utils;

namespace TiktokenSharp
{
    public class CoreBPE
    {
        private Dictionary<string, int> _specialTokensEncoder { get; set; }

        private Dictionary<ReadOnlyMemory<byte>, int> _encoder { get; set; }

        // TODO Cache？
        //private ConcurrentDictionary<char[], List<int>> _cache { get; set; }
        //private MemoryCache _cache = MemoryCache.Default;

        private Regex _specialRegex { get; set; }

        private Regex _regex { get; set; }


        private Lazy<Dictionary<int, ReadOnlyMemory<byte>>> _lazyDecoder;

        private Dictionary<int, ReadOnlyMemory<byte>> Decoder => _lazyDecoder.Value;



        private Dictionary<int, ReadOnlyMemory<byte>> _specialTokensDecoderBytes { get; set; }


        /// <summary>
        /// CoreBPE
        /// </summary>
        /// <param name="encoder"></param>
        /// <param name="specialTokensEncoder"></param>
        /// <param name="pattern"></param>
        public CoreBPE(Dictionary<ReadOnlyMemory<byte>, int> encoder, Dictionary<string, int> specialTokensEncoder, string pattern)
        {
            _encoder = encoder;
            //_cache = new ConcurrentDictionary<char[], List<int>>(new ReadOnlyMemoryComparer());
            _regex = new Regex(pattern, RegexOptions.Compiled);
            _specialRegex = new Regex(string.Join("|", specialTokensEncoder.Keys.Select(s => Regex.Escape(s))), RegexOptions.Compiled);
            _specialTokensEncoder = specialTokensEncoder;

            _lazyDecoder = new Lazy<Dictionary<int, ReadOnlyMemory<byte>>>(() =>
            {
                var decoder = _encoder.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

                if (_encoder.Count != decoder.Count)
                {
                    throw new ArgumentException("Encoder and decoder sizes don't match");
                }

                return decoder;
            });

            _specialTokensDecoderBytes = specialTokensEncoder.ToDictionary(kvp => kvp.Value, kvp => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(kvp.Key));



        }

#if NET7_0_OR_GREATER
        public (List<int>, int) EncodeNative(string text, HashSet<string> allowedSpecial, HashSet<string> disallowedSpecial)
        {
            return EncodeNative(text.AsSpan(), allowedSpecial, disallowedSpecial);
        }

        internal (List<int>, int) EncodeNative(ReadOnlySpan<char> text, HashSet<string> allowedSpecial, HashSet<string> disallowedSpecial)
        {
            var ret = new List<int>();

            int lastPieceTokenLen = 0;
            int currentIndex = 0;

            bool checkSpecial = allowedSpecial?.Count > 0 || disallowedSpecial?.Count > 0;
            var enumerator = _specialRegex.EnumerateMatches(text);
            // Use ArrayPool for all piece allocations to avoid per-match allocation
            byte[] rentedPieceBuffer = ArrayPool<byte>.Shared.Rent(768);

            try
            {
                while (currentIndex < text.Length)
                {
                    int nextMatchStart = text.Length;

                    if (checkSpecial && enumerator.MoveNext())
                    {
                        var current = enumerator.Current;
                        var special = text.Slice(current.Index, current.Length).ToString();
                        if (disallowedSpecial != null && disallowedSpecial.Contains(special))
                            throw new InvalidOperationException(special);
                        if (allowedSpecial != null && allowedSpecial.Contains(special))
                            nextMatchStart = current.Index;
                    }

                    ReadOnlySpan<char> currentSpan = text.Slice(currentIndex, nextMatchStart - currentIndex);
                    foreach (var match in _regex.EnumerateMatches(currentSpan))
                    {
                        var charSpan = currentSpan.Slice(match.Index, match.Length);

                        // Encode to UTF8 bytes
                        int maxByteCount = charSpan.Length * 3;

                        // Ensure rented buffer is large enough
                        if (rentedPieceBuffer.Length < maxByteCount)
                        {
                            var previousBuffer = rentedPieceBuffer;
                            rentedPieceBuffer = ArrayPool<byte>.Shared.Rent(maxByteCount);
                            ArrayPool<byte>.Shared.Return(previousBuffer);
                        }

                        int byteCount = Encoding.UTF8.GetBytes(charSpan, rentedPieceBuffer);
                        ReadOnlyMemory<byte> piece = new ReadOnlyMemory<byte>(rentedPieceBuffer, 0, byteCount);

                        if (_encoder.TryGetValue(piece, out int token))
                        {
                            lastPieceTokenLen = 1;
                            ret.Add(token);
                        }
                        else
                        {
                            // Pass ReadOnlyMemory directly - no array copy needed!
                            BytePairEncoding.BytePairEncodeInto(piece, _encoder, ret);
                            lastPieceTokenLen = ret.Count; // Approximate
                        }
                    }

                        currentIndex = nextMatchStart;

                    if (currentIndex < text.Length)
                    {
                        var match = enumerator.Current;
                        var pieceSpan = text.Slice(currentIndex, match.Length);

                        // Try to avoid ToString for special token lookup
                        if (_specialTokensEncoder.Count > 0)
                        {
                            var pieceStr = pieceSpan.ToString();
                            if (_specialTokensEncoder.TryGetValue(pieceStr, out int token))
                            {
                                ret.Add(token);
                                currentIndex += match.Length;
                                lastPieceTokenLen = 0;
                            }
                        }
                    }
                }

                return (ret, lastPieceTokenLen);
            }
            finally
            {
                // Return rented buffer
                ArrayPool<byte>.Shared.Return(rentedPieceBuffer);
            }
        }

#else 
        public (List<int>, int) EncodeNative(string text, HashSet<string> allowedSpecial, HashSet<string> disallowedSpecial)
        {
            Regex specialRegex = _specialRegex;
            Regex regex = _regex;
            var ret = new List<int>();

            int start = 0;
            int lastPieceTokenLen = 0;
            while (true)
            {
                Match nextSpecial;
                int startFind = start;
                while (true)
                {
                    nextSpecial = specialRegex.Match(text, startFind);
                    if (!nextSpecial.Success) break;
                    var currentText = text.Substring(nextSpecial.Index, nextSpecial.Length);

                    if (allowedSpecial != null && allowedSpecial.Contains(currentText))
                    {
                        break;
                    }
                    if (disallowedSpecial != null && disallowedSpecial.Contains(currentText))
                    {
                        throw new InvalidOperationException(currentText);
                    }
                    startFind = nextSpecial.Index + 1;
                }
                int end = nextSpecial.Success ? nextSpecial.Index : text.Length;

                foreach (Match mat in regex.Matches(text.Substring(start, end - start)))
                {
                    var piece = Encoding.UTF8.GetBytes(mat.Value);
                    if (_encoder.TryGetValue(piece, out int token))
                    {
                        lastPieceTokenLen = 1;
                        ret.Add(token);
                        continue;
                    }
                    var tokens = BytePairEncoding.BytePairEncode(piece, _encoder);
                    lastPieceTokenLen = tokens.Count;
                    ret.AddRange(tokens);
                }

                if (nextSpecial.Success)
                {
                    var piece = nextSpecial.Value;
                    var token = _specialTokensEncoder[piece];
                    ret.Add(token);
                    start = nextSpecial.Index + nextSpecial.Length;
                    lastPieceTokenLen = 0;
                }
                else
                {
                    break;
                }
            }

            return (ret, lastPieceTokenLen);
        }
#endif


        internal string DecodeText(List<int> tokens)
        {
            if (tokens.Count == 0)
                return string.Empty;

            var decoder = Decoder;
#if NETSTANDARD2_0
            byte[]? rented = ArrayPool<byte>.Shared.Rent(512);
            Span<byte> bytes = rented;
#else
            byte[]? rented = null;
            Span<byte> bytes = stackalloc byte[512];
#endif
            int written = 0;
            try
            {
                foreach (int token in tokens)
                {
                    if (!decoder.TryGetValue(token, out var piece) && !_specialTokensDecoderBytes.TryGetValue(token, out piece))
                        continue;

                    int required = checked(written + piece.Length);
                    if (required > bytes.Length)
                    {
                        int capacity = bytes.Length <= int.MaxValue / 2 ? Math.Max(required, bytes.Length * 2) : required;
                        var next = ArrayPool<byte>.Shared.Rent(capacity);
                        bytes.Slice(0, written).CopyTo(next);
                        if (rented != null)
                            ArrayPool<byte>.Shared.Return(rented);
                        bytes = rented = next;
                    }
                    piece.Span.CopyTo(bytes.Slice(written));
                    written = required;
                }
#if NETSTANDARD2_0
                return Encoding.UTF8.GetString(rented!, 0, written);
#else
                return Encoding.UTF8.GetString(bytes.Slice(0, written));
#endif
            }
            finally
            {
                if (rented != null)
                    ArrayPool<byte>.Shared.Return(rented);
            }
        }

        public List<ReadOnlyMemory<byte>> DecodeNative(IList<int> tokens)
        {
            var ret = new List<ReadOnlyMemory<byte>>(tokens.Count);
            foreach (var token in tokens)
            {
                ReadOnlyMemory<byte> tokenBytes = new ReadOnlyMemory<byte>();
                if (Decoder.TryGetValue(token, out var value))
                {
                    tokenBytes = value;
                }
                else
                {
                    if (_specialTokensDecoderBytes.TryGetValue(token, out var specialBytes))
                    {
                        tokenBytes = specialBytes;
                    }
                }

                if (tokenBytes.Length > 0)
                {
                    ret.Add(tokenBytes);
                }
            }
            return ret;
        }

#if NET7_0_OR_GREATER
        public int CountTokens(ReadOnlySpan<char> text, HashSet<string> allowedSpecial, HashSet<string> disallowedSpecial)
        {
            int count = 0;
            var textSpan = text;
            int currentIndex = 0;

            bool checkSpecial = allowedSpecial?.Count > 0 || disallowedSpecial?.Count > 0;
            var enumerator = _specialRegex.EnumerateMatches(textSpan);
            // Use ArrayPool for all piece allocations to avoid per-match allocation
            byte[] rentedPieceBuffer = ArrayPool<byte>.Shared.Rent(768);

            try
            {
                while (currentIndex < text.Length)
                {
                    int nextMatchStart = text.Length;

                    if (checkSpecial && enumerator.MoveNext())
                    {
                        var current = enumerator.Current;
                        var special = textSpan.Slice(current.Index, current.Length).ToString();
                        if (disallowedSpecial != null && disallowedSpecial.Contains(special))
                            throw new InvalidOperationException(special);
                        if (allowedSpecial != null && allowedSpecial.Contains(special))
                            nextMatchStart = current.Index;
                    }

                    ReadOnlySpan<char> currentSpan = textSpan.Slice(currentIndex, nextMatchStart - currentIndex);
                    foreach (var match in _regex.EnumerateMatches(currentSpan))
                    {
                        var charSpan = currentSpan.Slice(match.Index, match.Length);

                        // Encode to UTF8 bytes
                        int maxByteCount = charSpan.Length * 3;

                        // Ensure rented buffer is large enough
                        if (rentedPieceBuffer.Length < maxByteCount)
                        {
                            var previousBuffer = rentedPieceBuffer;
                            rentedPieceBuffer = ArrayPool<byte>.Shared.Rent(maxByteCount);
                            ArrayPool<byte>.Shared.Return(previousBuffer);
                        }

                        int byteCount = Encoding.UTF8.GetBytes(charSpan, rentedPieceBuffer);
                        ReadOnlyMemory<byte> piece = new ReadOnlyMemory<byte>(rentedPieceBuffer, 0, byteCount);

                        if (_encoder.ContainsKey(piece))
                        {
                            count++;
                        }
                        else
                        {
                            count += BytePairEncoding.BytePairEncodeCount(piece, _encoder);
                        }
                    }

                    currentIndex = nextMatchStart;

                    if (currentIndex < text.Length)
                    {
                        var match = enumerator.Current;
                        var pieceSpan = textSpan.Slice(currentIndex, match.Length);

                        // Try to avoid ToString for special token lookup
                        if (_specialTokensEncoder.Count > 0)
                        {
                            var pieceStr = pieceSpan.ToString();
                            if (_specialTokensEncoder.ContainsKey(pieceStr))
                            {
                                count++;
                                currentIndex += match.Length;
                            }
                        }
                    }
                }

                return count;
            }
            finally
            {
                // Return rented buffer
                ArrayPool<byte>.Shared.Return(rentedPieceBuffer);
            }
        }
#else
        public int CountTokens(string text, HashSet<string> allowedSpecial, HashSet<string> disallowedSpecial)
        {
            Regex specialRegex = _specialRegex;
            Regex regex = _regex;
            int count = 0;

            int start = 0;
            while (true)
            {
                Match nextSpecial;
                int startFind = start;
                while (true)
                {
                    nextSpecial = specialRegex.Match(text, startFind);
                    if (!nextSpecial.Success) break;
                    var currentText = text.Substring(nextSpecial.Index, nextSpecial.Length);

                    if (allowedSpecial != null && allowedSpecial.Contains(currentText))
                    {
                        break;
                    }
                    if (disallowedSpecial != null && disallowedSpecial.Contains(currentText))
                    {
                        throw new InvalidOperationException(currentText);
                    }
                    startFind = nextSpecial.Index + 1;
                }
                int end = nextSpecial.Success ? nextSpecial.Index : text.Length;

                foreach (Match mat in regex.Matches(text.Substring(start, end - start)))
                {
                    var piece = Encoding.UTF8.GetBytes(mat.Value);
                    if (_encoder.ContainsKey(piece))
                    {
                        count++;
                        continue;
                    }
                    count += BytePairEncoding.BytePairEncodeCount(piece, _encoder);
                }

                if (nextSpecial.Success)
                {
                    count++;
                    start = nextSpecial.Index + nextSpecial.Length;
                }
                else
                {
                    break;
                }
            }

            return count;
        }
#endif
    }
}
