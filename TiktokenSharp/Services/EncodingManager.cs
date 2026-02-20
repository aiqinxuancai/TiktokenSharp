using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
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

        public string PBEFileDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "bpe");


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
            { "gpt-5-", "o200k_base" },
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

        /// <summary>
        /// Gets the encoding settings for the specified model or encoding name.
        /// </summary>
        /// <param name="modelOrEncodingName">The model name or encoding name</param>
        /// <returns>The encoding setting model</returns>
        public EncodingSettingModel GetEncodingSetting(string modelOrEncodingName)
        {
            string encodingName = GetEncodingNameForModel(modelOrEncodingName);
            return GetEncoding(encodingName);
        }

        /// <summary>
        /// Asynchronously gets the encoding settings for the specified model or encoding name.
        /// </summary>
        /// <param name="modelOrEncodingName">The model name or encoding name</param>
        /// <returns>A task containing the encoding setting model</returns>
        public async Task<EncodingSettingModel> GetEncodingSettingAsync(string modelOrEncodingName)
        {
            string encodingName = GetEncodingNameForModel(modelOrEncodingName);
            return await GetEncodingAsync(encodingName);
        }




        /// <summary>
        /// Get encoding setting with encoding name.
        /// </summary>
        /// <param name="encodingName">cl100k_base p50k_base ...</param>
        /// <returns></returns>
        public EncodingSettingModel GetEncoding(string encodingName)
        {
            if (!string.IsNullOrEmpty(encodingName))
            {
                switch (encodingName)
                {
                    case "gpt2":
                        {
                            //TODO
                            throw new NotImplementedException();
                        }
                    case "r50k_base":
                        {
                            //TODO
                            throw new NotImplementedException(); ;
                        }
                    case "p50k_base":
                        {
                            return p50k_base().Result;
                        }
                    case "p50k_edit":
                        {
                            //TODO
                            throw new NotImplementedException();
                        }
                    case "cl100k_base":
                        {
                            return cl100k_base().Result;
                        }
                    case "o200k_base":
                        {
                            return o200k_base().Result;
                        }


                    default:
                        throw new NotImplementedException();
                }

            }
            else
            {
                throw new NotImplementedException("Unsupported model");
            }
        }





        /// <summary>
        /// Get encoding setting with encoding name.
        /// </summary>
        /// <param name="encodingName">cl100k_base p50k_base ...</param>
        /// <returns></returns>
        public async Task<EncodingSettingModel> GetEncodingAsync(string encodingName)
        {
            if (!string.IsNullOrEmpty(encodingName))
            {
                switch (encodingName)
                {
                    case "gpt2":
                        {
                            //TODO
                            throw new NotImplementedException();
                        }
                    case "r50k_base":
                        {
                            //TODO
                            throw new NotImplementedException(); ;
                        }
                    case "p50k_base":
                        {
                            return await p50k_base();
                        }
                    case "p50k_edit":
                        {
                            //TODO
                            throw new NotImplementedException();
                        }
                    case "cl100k_base":
                        {
                            return await cl100k_base();
                        }
                    case "o200k_base":
                        {
                            return await o200k_base();
                        }
                    default:
                        throw new NotImplementedException();
                }

            }
            else
            {
                throw new NotImplementedException("Unsupported model");
            }
        }



        private Dictionary<byte[], int> LoadTikTokenBpeFromLocal(string tikTokenBpeFile)
        {
            var contents = File.ReadAllLines(tikTokenBpeFile, Encoding.UTF8);
            var bpeDict = new Dictionary<byte[], int>(contents.Length, new ByteArrayComparer());

            foreach (var line in contents.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                var tokens = line.Split();
                bpeDict.Add(Convert.FromBase64String(tokens[0]), int.Parse(tokens[1]));
            }

            return bpeDict;
        }

        private async Task<Dictionary<ReadOnlyMemory<byte>, int>> LoadTikTokenBpe(string tikTokenBpeFile)
        {
            string localFilePath;
            if (tikTokenBpeFile.StartsWith("http"))
            {
                var fileName = Path.GetFileName(tikTokenBpeFile);
                var saveDir = PBEFileDirectory; //Path.Combine(Path.GetTempPath(), "bpe");

                try
                {
                    //If an exception occurs, it means that the folder cannot be created. Change the storage folder to the Temp directory.
                    if (!Directory.Exists(saveDir))
                    {
                        Directory.CreateDirectory(saveDir);
                    }
                }
                catch
                {
                    saveDir = Path.GetTempPath();
                }


                localFilePath = Path.Combine(saveDir, fileName);
                if (!File.Exists(localFilePath))
                {
                    using (var client = new HttpClient())
                    {
                        //client.DownloadFile(tikTokenBpeFile, localFilePath);
                        //File.WriteAllBytes(localFilePath, data);
                        var data = await client.GetByteArrayAsync(tikTokenBpeFile);
                        File.WriteAllBytes(localFilePath, data);
                    }
                }
            }
            else
            {
                localFilePath = tikTokenBpeFile;
            }
            var bpeDict = new Dictionary<ReadOnlyMemory<byte>, int>(new ReadOnlyMemoryComparer());

            try
            {
                var lines = File.ReadAllLines(localFilePath, Encoding.UTF8);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var tokens = line.Split(' ');
                    if (tokens.Length != 2)
                    {
                        throw new FormatException($"Invalid file format: {localFilePath}");
                    }

                    bpeDict[Convert.FromBase64String(tokens[0])] = int.Parse(tokens[1]);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load TikTokenBpe from {localFilePath}: {ex.Message}", ex);
            }

            return bpeDict;
        }


        private async Task<EncodingSettingModel> cl100k_base()
        {
            //When using the mod for the first time, the pbe file will be downloaded over the network.
            var mergeable_ranks = await LoadTikTokenBpe("https://openaipublic.blob.core.windows.net/encodings/cl100k_base.tiktoken");
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


        private async Task<EncodingSettingModel> o200k_base()
        {
            //When using the mod for the first time, the pbe file will be downloaded over the network.
            var mergeable_ranks = await LoadTikTokenBpe("https://openaipublic.blob.core.windows.net/encodings/o200k_base.tiktoken");
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


        private async Task<EncodingSettingModel> p50k_base()
        {
            //When using the mod for the first time, the pbe file will be downloaded over the network.
            var mergeable_ranks = await LoadTikTokenBpe("https://openaipublic.blob.core.windows.net/encodings/p50k_base.tiktoken");
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
