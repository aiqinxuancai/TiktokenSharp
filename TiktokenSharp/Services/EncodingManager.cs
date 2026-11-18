using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TiktokenSharp.Model;
using TiktokenSharp.Utils;

namespace TiktokenSharp.Services
{
    internal class EncodingManager
    {

        private static readonly Lazy<EncodingManager> _instance =
            new Lazy<EncodingManager>(() => new EncodingManager());

        public static EncodingManager Instance
        {
            get { return _instance.Value; }
        }

        private sealed class CachedEncoding
        {
            internal readonly SemaphoreSlim Gate = new SemaphoreSlim(1);
            internal WeakReference<EncodingSettingModel>? Setting;
            internal string? Path;
            internal long Length;
            internal DateTime LastWrite;
        }

        private readonly Dictionary<string, CachedEncoding> _encodings = new Dictionary<string, CachedEncoding>
        {
            ["cl100k_base"] = new CachedEncoding(),
            ["o200k_base"] = new CachedEncoding(),
            ["p50k_base"] = new CachedEncoding()
        };


        const string ENDOFTEXT = "<|endoftext|>";
        const string FIM_PREFIX = "<|fim_prefix|>";
        const string FIM_MIDDLE = "<|fim_middle|>";
        const string FIM_SUFFIX = "<|fim_suffix|>";
        const string ENDOFPROMPT = "<|endofprompt|>";


        private static readonly Dictionary<string, string> MODEL_PREFIX_TO_ENCODING = new Dictionary<string, string>()
        {
            { "o1-", "o200k_base" },
            { "o3-", "o200k_base" },
            { "o4-mini-", "o200k_base" },
            // chat
            { "gpt-5", "o200k_base" },
            { "gpt-6", "o200k_base" },  // Local estimates until tiktoken publishes an official mapping.
            { "gpt-4.5-", "o200k_base" },
            { "gpt-4.1-", "o200k_base" },
            { "chatgpt-4o-", "o200k_base" },
            { "gpt-4o-", "o200k_base" },  // e.g., gpt-4o-2024-05-13
            { "gpt-4-", "cl100k_base" },  // e.g., gpt-4-0314, etc., plus gpt-4-32k
            { "gpt-3.5-turbo-", "cl100k_base" },  // e.g, gpt-3.5-turbo-0301, -0401, etc.
            { "gpt-35-turbo-", "cl100k_base" },  // Azure deployment name
            // fine-tuned
            { "ft:gpt-4o", "o200k_base" },
            { "ft:gpt-4", "cl100k_base" },
            { "ft:gpt-3.5-turbo", "cl100k_base" },
            { "ft:davinci-002", "cl100k_base" },
            { "ft:babbage-002", "cl100k_base" },
        };


        private static readonly Dictionary<string, string> MODEL_TO_ENCODING = new Dictionary<string, string>()
        {
            // reasoning
            { "o1", "o200k_base" },
            { "o3", "o200k_base" },
            { "o4-mini", "o200k_base" },

            // chat
            { "gpt-5", "o200k_base" },
            { "gpt-4.5", "o200k_base" },
            { "gpt-4.1", "o200k_base" },
            { "gpt-4o", "o200k_base" },
            { "gpt-4", "cl100k_base" },
            { "gpt-3.5-turbo", "cl100k_base" },
            { "gpt-3.5", "cl100k_base" },  // Common shorthand
            { "gpt-35-turbo", "cl100k_base" },  // Azure deployment name
            // base
            { "davinci-002", "cl100k_base" },
            { "babbage-002", "cl100k_base" },
            // embeddings
            { "text-embedding-ada-002", "cl100k_base" },
            { "text-embedding-3-small", "cl100k_base" },
            { "text-embedding-3-large", "cl100k_base" },
            // DEPRECATED MODELS
            // text (DEPRECATED)
            { "text-davinci-003", "p50k_base" },
            { "text-davinci-002", "p50k_base" },
            { "text-davinci-001", "r50k_base" },
            { "text-curie-001", "r50k_base" },
            { "text-babbage-001", "r50k_base" },
            { "text-ada-001", "r50k_base" },
            { "davinci", "r50k_base" },
            { "curie", "r50k_base" },
            { "babbage", "r50k_base" },
            { "ada", "r50k_base" },
            // code (DEPRECATED)
            { "code-davinci-002", "p50k_base" },
            { "code-davinci-001", "p50k_base" },
            { "code-cushman-002", "p50k_base" },
            { "code-cushman-001", "p50k_base" },
            { "davinci-codex", "p50k_base" },
            { "cushman-codex", "p50k_base" },
            // edit (DEPRECATED)
            { "text-davinci-edit-001", "p50k_edit" },
            { "code-davinci-edit-001", "p50k_edit" },
            // old embeddings (DEPRECATED)
            { "text-similarity-davinci-001", "r50k_base" },
            { "text-similarity-curie-001", "r50k_base" },
            { "text-similarity-babbage-001", "r50k_base" },
            { "text-similarity-ada-001", "r50k_base" },
            { "text-search-davinci-doc-001", "r50k_base" },
            { "text-search-curie-doc-001", "r50k_base" },
            { "text-search-babbage-doc-001", "r50k_base" },
            { "text-search-ada-doc-001", "r50k_base" },
            { "code-search-babbage-code-001", "r50k_base" },
            { "code-search-ada-code-001", "r50k_base" },
            // open source
            { "gpt2", "gpt2" },
            { "gpt-2", "gpt2" },  // Maintains consistency with gpt-4
        };

        EncodingManager()
        {

        }
        /// <summary>
        /// Gets the encoding name for the specified model.
        /// </summary>
        /// <param name="modelName">The model name</param>
        /// <returns>The encoding name</returns>
        /// <exception cref="KeyNotFoundException">Thrown when the model cannot be mapped to an encoding</exception>
        private string GetEncodingNameForModel(string modelName)
        {
            if (string.IsNullOrEmpty(modelName))
            {
                throw new ArgumentNullException(nameof(modelName), "Model name cannot be null or empty");
            }

            // 1. Check if the input is already an encoding name
            if (MODEL_TO_ENCODING.Values.Contains(modelName))
            {
                return modelName;
            }

            // 2. Check if the model exists in the direct mapping
            if (MODEL_TO_ENCODING.TryGetValue(modelName, out string encodingName))
            {
                return encodingName;
            }

            // 3. Check if the model name matches any known prefix
            foreach (var prefix in MODEL_PREFIX_TO_ENCODING)
            {
                if (modelName.StartsWith(prefix.Key))
                {
                    return prefix.Value;
                }
            }

            // If no match is found, throw an exception
            throw new KeyNotFoundException(
                $"Could not automatically map {modelName} to a tokeniser. " +
                "Please use explicit encoding name."
            );
        }

        public EncodingSettingModel GetEncodingSetting(string model, string directory)
        {
            return GetEncodingSettingAsync(model, directory).Result;
        }

        public Task<EncodingSettingModel> GetEncodingSettingAsync(string model, string directory)
        {
            var name = GetEncodingNameForModel(model);
            if (!_encodings.TryGetValue(name, out var cache))
                throw new NotImplementedException();

            return LoadEncodingAsync(name, directory, cache);
        }

        private async Task<EncodingSettingModel> LoadEncodingAsync(string name, string directory, CachedEncoding cache)
        {
            await cache.Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var fileName = name + ".tiktoken";
                var url = "https://openaipublic.blob.core.windows.net/encodings/" + fileName;
                var source = await GetVocabularySourceAsync(url, fileName, directory).ConfigureAwait(false);
                var file = source.Path == null ? null : new FileInfo(source.Path);
                if (file != null && cache.Path == file.FullName && cache.Length == file.Length &&
                    cache.LastWrite == file.LastWriteTimeUtc && cache.Setting != null &&
                    cache.Setting.TryGetTarget(out var existing))
                    return existing;

                // Read the stamp before parsing so a concurrent replacement gets rechecked on the next call.
                var length = file?.Length ?? 0;
                var lastWrite = file?.LastWriteTimeUtc ?? default;
                var ranks = file != null ? LoadTikTokenBpeFromPath(file.FullName) :
                    LoadTikTokenBpeFromBytes(source.Data!, url);
                var setting = name switch
                {
                    "cl100k_base" => cl100k_base(ranks),
                    "o200k_base" => o200k_base(ranks),
                    "p50k_base" => p50k_base(ranks),
                    _ => throw new NotImplementedException()
                };
                if (file != null)
                {
                    cache.Path = file.FullName;
                    cache.Length = length;
                    cache.LastWrite = lastWrite;
                    cache.Setting = new WeakReference<EncodingSettingModel>(setting);
                }
                return setting;
            }
            finally
            {
                cache.Gate.Release();
            }
        }

        private async Task<(string? Path, byte[]? Data)> GetVocabularySourceAsync(string tikTokenBpeFile, string fileName, string directory)
        {
            var preferredDir = string.IsNullOrWhiteSpace(directory)
                ? Path.Combine(AppContext.BaseDirectory, "bpe")
                : directory;

            var candidateDirs = new[] { preferredDir, Path.GetTempPath() }
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            byte[]? data = null;

            foreach (var candidateDir in candidateDirs)
            {
                try
                {
                    var localFilePath = Path.Combine(candidateDir, fileName);
                    if (File.Exists(localFilePath))
                        return (localFilePath, null);

                    Directory.CreateDirectory(candidateDir);
                    data ??= await DownloadTikTokenBpeAsync(tikTokenBpeFile).ConfigureAwait(false);
                    TryWriteAllBytes(localFilePath, data);
                    return (localFilePath, null);
                }
                catch
                {
                    // Try next candidate directory.
                }
            }

            return (null, data ?? await DownloadTikTokenBpeAsync(tikTokenBpeFile).ConfigureAwait(false));
        }

        private static void TryWriteAllBytes(string filePath, byte[] data)
        {
            // Publish a complete file so another process cannot cache a partially written vocabulary.
            string temporaryPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            try
            {
                using (stream)
                    stream.Write(data, 0, data.Length);
                try
                {
                    File.Move(temporaryPath, filePath);
                }
                catch (IOException) when (File.Exists(filePath))
                {
                    // A concurrent writer finished first.
                }
            }
            finally
            {
                File.Delete(temporaryPath);
            }
        }

        private async Task<byte[]> DownloadTikTokenBpeAsync(string tikTokenBpeFile)
        {
            using (var client = new HttpClient())
            {
                return await client.GetByteArrayAsync(tikTokenBpeFile).ConfigureAwait(false);
            }
        }

        private Dictionary<ReadOnlyMemory<byte>, int> LoadTikTokenBpeFromPath(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                return ParseTikTokenBpe(stream, path);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load TikTokenBpe from {path}: {ex.Message}", ex);
            }
        }

        private Dictionary<ReadOnlyMemory<byte>, int> LoadTikTokenBpeFromBytes(byte[] data, string source)
        {
            try
            {
                using var stream = new MemoryStream(data, writable: false);
                return ParseTikTokenBpe(stream, source);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load TikTokenBpe from {source}: {ex.Message}", ex);
            }
        }

        private static Dictionary<ReadOnlyMemory<byte>, int> ParseTikTokenBpe(Stream stream, string source)
        {
            // Built-in vocabulary rows average 16-19 bytes. Cap the initial reservation for custom files.
            int capacity = (int)Math.Min(stream.Length / 16, 200000);
            var ranks = new Dictionary<ReadOnlyMemory<byte>, int>(capacity, new ReadOnlyMemoryComparer());
            using var reader = new StreamReader(stream, Encoding.UTF8);
#if !NETSTANDARD2_0
            byte[] block = Array.Empty<byte>();
            int blockSize = (int)Math.Min(stream.Length, 16384);
            int used = 0;
#endif
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                int separator = line.IndexOf(' ');
                if (separator < 0 || line.IndexOf(' ', separator + 1) >= 0)
                    throw new FormatException($"Invalid file format: {source}");
#if NETSTANDARD2_0
                ranks[Convert.FromBase64String(line.Substring(0, separator))] = int.Parse(line.Substring(separator + 1));
#else
                int maxBytes = separator / 4 * 3;
                if (block.Length - used < maxBytes)
                {
                    block = new byte[Math.Max(blockSize, maxBytes)];
                    used = 0;
                }
                if (!Convert.TryFromBase64Chars(line.AsSpan(0, separator), block.AsSpan(used), out int length))
                    throw new FormatException($"Invalid base64 token in {source}");

                ranks[new ReadOnlyMemory<byte>(block, used, length)] = int.Parse(line.AsSpan(separator + 1));
                used += length;
#endif
            }
            return ranks;
        }


        private static EncodingSettingModel cl100k_base(Dictionary<ReadOnlyMemory<byte>, int> mergeable_ranks)
        {
            var special_tokens = new Dictionary<string, int>{
                                    { ENDOFTEXT, 100257},
                                    { FIM_PREFIX, 100258},
                                    { FIM_MIDDLE, 100259},
                                    { FIM_SUFFIX, 100260},
                                    { ENDOFPROMPT, 100276}
                                };

            return new EncodingSettingModel()
            {
                Name = "cl100k_base",
                PatStr = @"(?i:'s|'t|'re|'ve|'m|'ll|'d)|[^\r\n\p{L}\p{N}]?\p{L}+|\p{N}{1,3}| ?[^\s\p{L}\p{N}]+[\r\n]*|\s*[\r\n]+|\s+(?!\S)|\s+",
                MergeableRanks = mergeable_ranks,
                SpecialTokens = special_tokens
            };
        }


        private static EncodingSettingModel o200k_base(Dictionary<ReadOnlyMemory<byte>, int> mergeable_ranks)
        {
            var special_tokens = new Dictionary<string, int>{
                                    { ENDOFTEXT, 199999},
                                    { ENDOFPROMPT, 200018}
                                };

            string[] patterns = new string[]
            {
                @"[^\r\n\p{L}\p{N}]?[\p{Lu}\p{Lt}\p{Lm}\p{Lo}\p{M}]*[\p{Ll}\p{Lm}\p{Lo}\p{M}]+(?i:'s|'t|'re|'ve|'m|'ll|'d)?",
                @"[^\r\n\p{L}\p{N}]?[\p{Lu}\p{Lt}\p{Lm}\p{Lo}\p{M}]+[\p{Ll}\p{Lm}\p{Lo}\p{M}]*(?i:'s|'t|'re|'ve|'m|'ll|'d)?",
                @"\p{N}{1,3}",
                @" ?[^\s\p{L}\p{N}]+[\r\n/]*",
                @"\s*[\r\n]+",
                @"\s+(?!\S)",
                @"\s+",
            };

            string patStr = string.Join("|", patterns);


            return new EncodingSettingModel()
            {
                Name = "o200k_base",
                PatStr = patStr,
                MergeableRanks = mergeable_ranks,
                SpecialTokens = special_tokens
            };
        }


        private static EncodingSettingModel p50k_base(Dictionary<ReadOnlyMemory<byte>, int> mergeable_ranks)
        {
            var special_tokens = new Dictionary<string, int>{
                                    { ENDOFTEXT, 50256}
                                };

            return new EncodingSettingModel()
            {
                Name = "p50k_base",
                ExplicitNVocab = 50281,
                PatStr = @"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+",
                MergeableRanks = mergeable_ranks,
                SpecialTokens = special_tokens
            };
        }
    }
}
