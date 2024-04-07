using System;
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

        // TODO private max_token_value ??
        private Dictionary<byte[], int> _encoder { get; set; }

        private Regex _specialRegex { get; set; }

        private Regex _regex { get; set; }


        private Lazy<Dictionary<int, byte[]>> _lazyDecoder;

        private Dictionary<int, byte[]> Decoder => _lazyDecoder.Value;


        private Dictionary<int, string> _specialTokensDecoder { get; set; }


        /// <summary>
        /// CoreBPE
        /// </summary>
        /// <param name="encoder"></param>
        /// <param name="specialTokensEncoder"></param>
        /// <param name="pattern"></param>
        public CoreBPE(Dictionary<byte[], int> encoder, Dictionary<string, int> specialTokensEncoder, string pattern)
        {
            _encoder = encoder;
            _regex = new Regex(pattern, RegexOptions.Compiled);
            _specialRegex = new Regex(string.Join("|", specialTokensEncoder.Keys.Select(s => Regex.Escape(s))), RegexOptions.Compiled);
            _specialTokensEncoder = specialTokensEncoder;

            _lazyDecoder = new Lazy<Dictionary<int, byte[]>>(() =>
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
        public (List<int>, int) EncodeNative(string text, HashSet<string> allowedSpecial)
        {
            Regex specialRegex = _specialRegex;
            Regex regex = _regex;
            var ret = new List<int>();

            ReadOnlySpan<char> textSpan = text.AsSpan();
            int lastPieceTokenLen = 0;
            int currentIndex = 0;

            var enumerator = specialRegex.EnumerateMatches(text);

            while (currentIndex < text.Length)
            {
                int nextMatchStart = text.Length;

                if (enumerator.MoveNext())
                {
                    var current = enumerator.Current;
                    if (allowedSpecial.Contains(textSpan.Slice(current.Index, current.Length).ToString()))
                    {
                        nextMatchStart = current.Index;
                    }
                }

                ReadOnlySpan<char> currentSpan = textSpan.Slice(currentIndex, nextMatchStart - currentIndex);
                foreach (var match in regex.EnumerateMatches(currentSpan))
                {
                    var piece = Encoding.UTF8.GetBytes(currentSpan.Slice(match.Index, match.Length).ToString());
                    if (_encoder.TryGetValue(piece, out int token))
                    {
                        lastPieceTokenLen = 1;
                        ret.Add(token);
                    }
                    else
                    {
                        var tokens = BytePairEncoding.BytePairEncode(piece, _encoder);
                        lastPieceTokenLen = tokens.Count;
                        ret.AddRange(tokens);
                    }
                }

                currentIndex = nextMatchStart;

                if (currentIndex < text.Length)
                {
                    var match = enumerator.Current;
                    var pieceSpan = textSpan.Slice(currentIndex, match.Length);
                    if (_specialTokensEncoder.TryGetValue(pieceSpan.ToString(), out int token))
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
        public (List<int>, int) EncodeNative(string text, HashSet<string> allowedSpecial)
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
                    if (allowedSpecial.Contains(text.Substring(nextSpecial.Index, nextSpecial.Length))) break;
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


        public byte[] DecodeNative(int[] tokens)
        {
            var ret = new List<byte>(tokens.Length * 2);
            foreach (var token in tokens)
            {
                byte[] tokenBytes = { };
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
                    ret.AddRange(tokenBytes);
                } 
            }
            return ret.ToArray();
        }
    }
}
