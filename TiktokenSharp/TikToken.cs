using Microsoft.VisualBasic;
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
using TiktokenSharp.Utils;

namespace TiktokenSharp
{
    public enum SpecialTokensMode
    {
        None,       
        All,        
        Custom      
    }

    public class TikToken
    {

        /// <summary>
        /// You can set this item before EncodingForModel to specify the location for storing and downloading the bpe file. If not set, it defaults to the AppContext.BaseDirectory\bpe directory.
        /// </summary>
        public static string PBEFileDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "bpe");

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

        private CoreBPE _corePBE;

        private EncodingSettingModel _setting;

        public TikToken(EncodingSettingModel setting)
        {
            if (setting.ExplicitNVocab != null)
            {
                if (setting.SpecialTokens.Count + setting.MergeableRanks.Count != setting.ExplicitNVocab)
                {
                    throw new ArgumentException("SpecialTokens + MergeableRanks counts must equal ExplicitNVocab.");
                }

                if (setting.MaxTokenValue != setting.ExplicitNVocab - 1)
                {
                    throw new ArgumentException("MaxTokenValue must be equal to ExplicitNVocab - 1.");
                }
            }

            _corePBE = new CoreBPE(setting.MergeableRanks, setting.SpecialTokens, setting.PatStr);
            _setting = setting;
        }

        public List<int> Encode(string text,
            HashSet<string> allowedSpecial = null,
            HashSet<string> disallowedSpecial = null,
            SpecialTokensMode allowedSpecialMode = SpecialTokensMode.Custom,
            SpecialTokensMode disallowedSpecialMode = SpecialTokensMode.Custom)
        {
            HashSet<string> effectiveAllowed;
            if (allowedSpecialMode == SpecialTokensMode.All)
            {
                effectiveAllowed = new HashSet<string>(_setting.SpecialTokens.Keys);
            }
            else if (allowedSpecialMode == SpecialTokensMode.None || allowedSpecial == null)
            {
                effectiveAllowed = new HashSet<string>();
            }
            else
            {
                effectiveAllowed = allowedSpecial;
            }

            HashSet<string> effectiveDisallowed;
            if (disallowedSpecialMode == SpecialTokensMode.All)
            {
                // py: disallowed_special = self.special_tokens_set - allowed_special
                effectiveDisallowed = new HashSet<string>(_setting.SpecialTokens.Keys);
                effectiveDisallowed.ExceptWith(effectiveAllowed);
            }
            else if (disallowedSpecialMode == SpecialTokensMode.None || disallowedSpecial == null)
            {
                effectiveDisallowed = new HashSet<string>();
            }
            else
            {
                effectiveDisallowed = disallowedSpecial;
            }

            return _corePBE.EncodeNative(text, effectiveAllowed, effectiveDisallowed).Item1;
        }

        public List<int> EncodeWithAllSpecialAllowed(string text)
        {
            return Encode(text, allowedSpecialMode: SpecialTokensMode.All, disallowedSpecialMode: SpecialTokensMode.None);
        }

        public List<int> EncodeWithNoSpecialChecks(string text)
        {
            return Encode(text, allowedSpecialMode: SpecialTokensMode.None, disallowedSpecialMode: SpecialTokensMode.None);
        }

        public List<int> EncodeDefault(string text)
        {
            return Encode(text);
        }

        public string Decode(List<int> tokens)
        {
            var ret = _corePBE.DecodeNative(tokens);
            string str = ByteHelper.ConvertByteListToString(ret);
            Utils.ByteMemoryListPool.Return(ret);
            return str;
        }

#if NET7_0_OR_GREATER
        /// <summary>
        /// Encodes text using ReadOnlySpan for zero-allocation performance
        /// </summary>
        public List<int> Encode(ReadOnlySpan<char> text,
            HashSet<string> allowedSpecial = null,
            HashSet<string> disallowedSpecial = null,
            SpecialTokensMode allowedSpecialMode = SpecialTokensMode.Custom,
            SpecialTokensMode disallowedSpecialMode = SpecialTokensMode.Custom)
        {
            HashSet<string> effectiveAllowed;
            if (allowedSpecialMode == SpecialTokensMode.All)
            {
                effectiveAllowed = new HashSet<string>(_setting.SpecialTokens.Keys);
            }
            else if (allowedSpecialMode == SpecialTokensMode.None || allowedSpecial == null)
            {
                effectiveAllowed = new HashSet<string>();
            }
            else
            {
                effectiveAllowed = allowedSpecial;
            }

            HashSet<string> effectiveDisallowed;
            if (disallowedSpecialMode == SpecialTokensMode.All)
            {
                effectiveDisallowed = new HashSet<string>(_setting.SpecialTokens.Keys);
                effectiveDisallowed.ExceptWith(effectiveAllowed);
            }
            else if (disallowedSpecialMode == SpecialTokensMode.None || disallowedSpecial == null)
            {
                effectiveDisallowed = new HashSet<string>();
            }
            else
            {
                effectiveDisallowed = disallowedSpecial;
            }

            return _corePBE.EncodeNative(text.ToString(), effectiveAllowed, effectiveDisallowed).Item1;
        }

        /// <summary>
        /// Counts tokens without allocating a list - efficient for getting token count only
        /// </summary>
        public int CountTokens(ReadOnlySpan<char> text,
            HashSet<string> allowedSpecial = null,
            HashSet<string> disallowedSpecial = null,
            SpecialTokensMode allowedSpecialMode = SpecialTokensMode.Custom,
            SpecialTokensMode disallowedSpecialMode = SpecialTokensMode.Custom)
        {
            HashSet<string> effectiveAllowed;
            if (allowedSpecialMode == SpecialTokensMode.All)
            {
                effectiveAllowed = new HashSet<string>(_setting.SpecialTokens.Keys);
            }
            else if (allowedSpecialMode == SpecialTokensMode.None || allowedSpecial == null)
            {
                effectiveAllowed = new HashSet<string>();
            }
            else
            {
                effectiveAllowed = allowedSpecial;
            }

            HashSet<string> effectiveDisallowed;
            if (disallowedSpecialMode == SpecialTokensMode.All)
            {
                effectiveDisallowed = new HashSet<string>(_setting.SpecialTokens.Keys);
                effectiveDisallowed.ExceptWith(effectiveAllowed);
            }
            else if (disallowedSpecialMode == SpecialTokensMode.None || disallowedSpecial == null)
            {
                effectiveDisallowed = new HashSet<string>();
            }
            else
            {
                effectiveDisallowed = disallowedSpecial;
            }

            return _corePBE.CountTokens(text, effectiveAllowed, effectiveDisallowed);
        }

        /// <summary>
        /// Counts tokens from a string without allocating a list
        /// </summary>
        public int CountTokens(string text,
            HashSet<string> allowedSpecial = null,
            HashSet<string> disallowedSpecial = null,
            SpecialTokensMode allowedSpecialMode = SpecialTokensMode.Custom,
            SpecialTokensMode disallowedSpecialMode = SpecialTokensMode.Custom)
        {
            return CountTokens(text.AsSpan(), allowedSpecial, disallowedSpecial, allowedSpecialMode, disallowedSpecialMode);
        }
#else
        /// <summary>
        /// Counts tokens without allocating a list - efficient for getting token count only
        /// </summary>
        public int CountTokens(string text,
            HashSet<string> allowedSpecial = null,
            HashSet<string> disallowedSpecial = null,
            SpecialTokensMode allowedSpecialMode = SpecialTokensMode.Custom,
            SpecialTokensMode disallowedSpecialMode = SpecialTokensMode.Custom)
        {
            HashSet<string> effectiveAllowed;
            if (allowedSpecialMode == SpecialTokensMode.All)
            {
                effectiveAllowed = new HashSet<string>(_setting.SpecialTokens.Keys);
            }
            else if (allowedSpecialMode == SpecialTokensMode.None || allowedSpecial == null)
            {
                effectiveAllowed = new HashSet<string>();
            }
            else
            {
                effectiveAllowed = allowedSpecial;
            }

            HashSet<string> effectiveDisallowed;
            if (disallowedSpecialMode == SpecialTokensMode.All)
            {
                effectiveDisallowed = new HashSet<string>(_setting.SpecialTokens.Keys);
                effectiveDisallowed.ExceptWith(effectiveAllowed);
            }
            else if (disallowedSpecialMode == SpecialTokensMode.None || disallowedSpecial == null)
            {
                effectiveDisallowed = new HashSet<string>();
            }
            else
            {
                effectiveDisallowed = disallowedSpecial;
            }

            return _corePBE.CountTokens(text, effectiveAllowed, effectiveDisallowed);
        }
#endif

    }
}
