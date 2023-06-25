using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TiktokenSharp.Model;
using TiktokenSharp.Services;

namespace TiktokenSharp
{
    public class TikToken
    {

        /// <summary>
        /// You can set this item before EncodingForModel to specify the location for storing and downloading the bpe file. If not set, it defaults to the AppContext.BaseDirectory\bpe directory.
        /// </summary>
        public static string PBEFileDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "bpe");

        /// <summary>
        /// get encoding with modelName
        /// </summary>
        /// <param name="modelName">gpt-3.5-turbo</param>
        /// <returns></returns>
        public static TikToken EncodingForModel(string modelName)
        {
            EncodingManager.Instance.PBEFileDirectory = PBEFileDirectory;
            var setting = EncodingManager.Instance.GetEncodingSetting(modelName);
            return new TikToken(setting);
        }


        


        /// <summary>
        /// get encoding with encoding name
        /// </summary>
        /// <param name="encodingName">cl100k_base</param>
        /// <returns></returns>
        public static TikToken GetEncoding(string encodingName)
        {
            EncodingManager.Instance.PBEFileDirectory = PBEFileDirectory;
            var setting = EncodingManager.Instance.GetEncodingSetting(encodingName);
            return new TikToken(setting);
        }


        /// <summary>
        /// get encoding with modelName
        /// </summary>
        /// <param name="modelName">gpt-3.5-turbo</param>
        /// <returns></returns>
        public static async Task<TikToken> EncodingForModelAsync(string modelName)
        {
            EncodingManager.Instance.PBEFileDirectory = PBEFileDirectory;
            var setting = await EncodingManager.Instance.GetEncodingSettingAsync(modelName);
            return new TikToken(setting);
        }

        /// <summary>
        /// get encoding with encoding name
        /// </summary>
        /// <param name="encodingName">cl100k_base</param>
        /// <returns></returns>
        public static async Task<TikToken> GetEncodingAsync(string encodingName)
        {
            EncodingManager.Instance.PBEFileDirectory = PBEFileDirectory;
            var setting = await EncodingManager.Instance.GetEncodingSettingAsync(encodingName);
            return new TikToken(setting);
        }

        public static Regex SpecialTokenRegex(HashSet<string> tokens)
        {
            var inner = string.Join("|", tokens.Select(Regex.Escape));
            return new Regex($"({inner})");
        }

        private CoreBPE _corePBE;

        private EncodingSettingModel _setting;

        public TikToken(EncodingSettingModel setting)
        {
            if (setting.ExplicitNVocab != null)
            {
                Debug.Assert(setting.SpecialTokens.Count + setting.MergeableRanks.Count == setting.ExplicitNVocab);
                Debug.Assert(setting.MaxTokenValue == setting.ExplicitNVocab - 1);
            }

            _corePBE = new CoreBPE(setting.MergeableRanks, setting.SpecialTokens, setting.PatStr);
            _setting = setting;
        }

        public HashSet<string> SpecialTokensSet()
        {
            return new HashSet<string>(_setting.SpecialTokens.Keys);
        }

        public List<int> Encode(string text, object allowedSpecial = null, object disallowedSpecial = null)
        {
            if (allowedSpecial == null)
            {
                allowedSpecial = new HashSet<string>();
            }
            if (disallowedSpecial == null)
            {
                disallowedSpecial = "all";
            }

            var allowedSpecialSet = allowedSpecial.Equals("all") ? SpecialTokensSet() : new HashSet<string>((IEnumerable<string>)allowedSpecial);
            var disallowedSpecialSet = disallowedSpecial.Equals("all") ? new HashSet<string>(SpecialTokensSet().Except(allowedSpecialSet)) : new HashSet<string>((IEnumerable<string>)disallowedSpecial);

            if (disallowedSpecialSet.Count() > 0)
            {
                var specialTokenRegex = SpecialTokenRegex(disallowedSpecialSet);
                var match = specialTokenRegex.Match(text);
                if (match.Success)
                {
                    throw new Exception(match.Value);
                }
            }

            return _corePBE.EncodeNative(text, allowedSpecialSet).Item1;
        }

        public string Decode(List<int> tokens)
        {
            var ret = _corePBE.DecodeNative(tokens.ToArray());
            string str = Encoding.UTF8.GetString(ret.ToArray());
            return str;
        }





    }
}
