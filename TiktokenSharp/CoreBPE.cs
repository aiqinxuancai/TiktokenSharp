using System;
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


        private Dictionary<int, string> _specialTokensDecoder { get; set; }


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

            _specialTokensDecoder = specialTokensEncoder.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

            var sortedTokenBytes = _encoder.Keys.ToList();
        }

#if NET7_0_OR_GREATER
        public (List<int>, int) EncodeNative(string text, HashSet<string> allowedSpecial, HashSet<string> disallowedSpecial)
        {
            var ret = new List<int>();

            var textSpan = text.AsMemory();
            int lastPieceTokenLen = 0;
            int currentIndex = 0;

            var enumerator = _specialRegex.EnumerateMatches(textSpan.Span);

            while (currentIndex < text.Length)
            {
                int nextMatchStart = text.Length;

                if (enumerator.MoveNext())
                {
   
                    var current = enumerator.Current;
                    var currentText = textSpan.Slice(current.Index, current.Length).ToString();

                    if (disallowedSpecial != null && disallowedSpecial.Contains(currentText))
                    {
                        throw new InvalidOperationException(currentText.ToString());
                    }
                    if (allowedSpecial != null && allowedSpecial.Contains(currentText))
                    {
                        nextMatchStart = current.Index;
                    }

                }

                //read only 

                ReadOnlyMemory<char> currentSpan = textSpan.Slice(currentIndex, nextMatchStart - currentIndex);
                foreach (var match in _regex.EnumerateMatches(currentSpan.Span))
                {
                    var charSpan = currentSpan.Slice(match.Index, match.Length);
                    //var byteSpan = ByteHelper.ConvertReadOnlyMemoryCharToByte(charSpan);

                    var piece = Encoding.UTF8.GetBytes(charSpan.ToString()); //TODO remove ToString
                    if (_encoder.TryGetValue(piece, out int token))
                    {
                        lastPieceTokenLen = 1;
                        ret.Add(token);
                    }
                    else
                    {
                        //TODO Cache？

                        //if (_cache.TryGetValue(piece, out List<int> cacheToken))
                        //{
                        //    ret.AddRange(cacheToken);
                        //    continue;
                        //}

                        var tokens = BytePairEncoding.BytePairEncode(piece, _encoder);
                        lastPieceTokenLen = tokens.Count;
                        ret.AddRange(tokens);

                        //_cache[piece] = tokens;
                    }
                }

                currentIndex = nextMatchStart;

                if (currentIndex < text.Length)
                {
                    var match = enumerator.Current;
                    var pieceSpan = textSpan.Slice(currentIndex, match.Length);
                    if (_specialTokensEncoder.TryGetValue(pieceSpan.ToString(), out int token)) //TODO remove ToString
                    {
                        ret.Add(token);
                        currentIndex += match.Length;
                        lastPieceTokenLen = 0;
                    }
                }
            }

            return (ret, lastPieceTokenLen);
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


        public List<ReadOnlyMemory<byte>> DecodeNative(int[] tokens)
        {
            var ret = new List<ReadOnlyMemory<byte>>(tokens.Length * 2);
            foreach (var token in tokens)
            {
                ReadOnlyMemory<byte> tokenBytes = new ReadOnlyMemory<byte>();
                if (Decoder.TryGetValue(token, out var value))
                {
                    tokenBytes = value;
                } 
                else
                {
                    if (_specialTokensDecoder.TryGetValue(token, out var valueS))
                    {
                        tokenBytes = UTF8Encoding.UTF8.GetBytes(valueS);
                    }
                }

                if (tokenBytes.Length > 0)
                {
                    ret.Add(tokenBytes);
                } 
            }
            return ret;
        }
    }
}
