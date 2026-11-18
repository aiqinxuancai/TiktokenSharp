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
        public static string PBEFileDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "bpe");

        /// <summary>
        /// get encoding with modelName
        /// </summary>
        /// <param name="modelName">gpt-3.5-turbo</param>
        /// <returns></returns>
        public static TikToken EncodingForModel(string modelName)
        {
            var setting = EncodingManager.Instance.GetEncodingSetting(modelName, PBEFileDirectory);
            return new TikToken(setting, shareCore: true);
        }

        /// <summary>
        /// get encoding with encoding name
        /// </summary>
        /// <param name="encodingName">cl100k_base</param>
        /// <returns></returns>
        public static TikToken GetEncoding(string encodingName)
        {
            var setting = EncodingManager.Instance.GetEncodingSetting(encodingName, PBEFileDirectory);
            return new TikToken(setting, shareCore: true);
        }

        /// <summary>
        /// get encoding with modelName
        /// </summary>
        /// <param name="modelName">gpt-3.5-turbo</param>
        /// <returns></returns>
        public static async Task<TikToken> EncodingForModelAsync(string modelName)
        {
            var setting = await EncodingManager.Instance.GetEncodingSettingAsync(modelName, PBEFileDirectory).ConfigureAwait(false);
            return new TikToken(setting, shareCore: true);
        }

        /// <summary>
        /// get encoding with encoding name
        /// </summary>
        /// <param name="encodingName">cl100k_base</param>
        /// <returns></returns>
        public static async Task<TikToken> GetEncodingAsync(string encodingName)
        {
            var setting = await EncodingManager.Instance.GetEncodingSettingAsync(encodingName, PBEFileDirectory).ConfigureAwait(false);
            return new TikToken(setting, shareCore: true);
        }

        private CoreBPE _corePBE;

        private EncodingSettingModel _setting;

        private static readonly ConditionalWeakTable<EncodingSettingModel, CoreBPE> BuiltInCores = new();
        private static readonly HashSet<string> EmptySpecialTokens = new();

        public TikToken(EncodingSettingModel setting) : this(setting, shareCore: false) { }

        private TikToken(EncodingSettingModel setting, bool shareCore)
        {
            _setting = setting;
            _corePBE = shareCore ? BuiltInCores.GetValue(setting, CreateCore) : CreateCore(setting);
        }

        private static CoreBPE CreateCore(EncodingSettingModel setting)
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

            return new CoreBPE(setting.MergeableRanks, setting.SpecialTokens, setting.PatStr);
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
                effectiveAllowed = EmptySpecialTokens;
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
                effectiveDisallowed = EmptySpecialTokens;
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
            return _corePBE.DecodeText(tokens);
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
                effectiveAllowed = EmptySpecialTokens;
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
                effectiveDisallowed = EmptySpecialTokens;
            }
            else
            {
                effectiveDisallowed = disallowedSpecial;
            }

            return _corePBE.EncodeNative(text, effectiveAllowed, effectiveDisallowed).Item1;
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
                effectiveAllowed = EmptySpecialTokens;
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
                effectiveDisallowed = EmptySpecialTokens;
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
                effectiveAllowed = EmptySpecialTokens;
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
                effectiveDisallowed = EmptySpecialTokens;
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
