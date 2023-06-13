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
        /// 
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

        //TODO add only get token counts func

        public List<int> EncodeNative(string text, HashSet<string> allowedSpecial)
        {
            Regex specialRegex = _specialRegex;
            Regex regex = _regex;
            var ret = new List<int>();

#if NET7_0_OR_GREATER
            var textSpan = text.AsSpan();
#endif
            int start = 0;
            int nextStart = 0;
            string nextSpecialValue = string.Empty;
            int end = text.Length;

            while (true)
            {
                int startFind = start;
                if (allowedSpecial.Count != 0)
                {
                    Match nextSpecial;
                    while (true)
                    {
                        nextSpecial = specialRegex.Match(text, startFind);
                        if (!nextSpecial.Success) break;
                        if (allowedSpecial.Contains(text.Substring(nextSpecial.Index, nextSpecial.Length))) break;
                        startFind = nextSpecial.Index + 1;
                    }
                    end = nextSpecial.Success ? nextSpecial.Index : text.Length;
                    nextStart = nextSpecial.Index + nextSpecial.Length;
                    nextSpecialValue = nextSpecial.Value;
                }

#if NET7_0_OR_GREATER 
                foreach (var mat in regex.EnumerateMatches(textSpan.Slice(start, end - start)))
                {
                    var v = textSpan.Slice(mat.Index, mat.Length).ToString();
                    var piece = Encoding.UTF8.GetBytes(v);
#else
                foreach (Match mat in regex.Matches(text.Substring(start, end - start)))
                {
                    var v = mat.Value;
                    var piece = Encoding.UTF8.GetBytes(mat.Value);
#endif

                    if (_encoder.TryGetValue(piece, out int token))
                    {
                        ret.Add(token);
                        continue;
                    }
                    var tokens = BytePairEncoding.BytePairEncode(piece, _encoder);
                    ret.AddRange(tokens);
                }

                if (end != text.Length)
                {
                    var token = _specialTokensEncoder[nextSpecialValue];
                    ret.Add(token);
                    start = nextStart;
                }
                else
                {
                    break;
                }
            }

            return ret;
        }

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
