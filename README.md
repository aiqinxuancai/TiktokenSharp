# TiktokenSharp

This library is a C# implementation of the token count calculation, referencing OpenAI's official Rust language version. Currently, the encoding algorithms for `o200k_base`, `cl100k_base`, and `p50k_base` have been implemented. You can directly obtain the corresponding encoding algorithm using the model name.

GPT-5.x models use `o200k_base`. GPT-6.x models, including `gpt-6.2-astra` and `gpt-6.2-sol`, use `o200k_base` for local text token estimates while the public tiktoken mapping omits GPT-6.x. [OpenAI tiktoken model mappings](https://github.com/openai/tiktoken/blob/main/tiktoken/model.py)

## Getting Started

TiktokenSharp is available as [NuGet package](https://www.nuget.org/packages/TiktokenSharp/).

```csharp
using TiktokenSharp;

//use model name
TikToken tikToken = TikToken.EncodingForModel("gpt-3.5-turbo");
var i = tikToken.Encode("hello world"); //[15339, 1917]
var d = tikToken.Decode(i); //hello world

//use encoding name
TikToken tikToken = TikToken.GetEncoding("cl100k_base");
var i = tikToken.Encode("hello world"); //[15339, 1917]
var d = tikToken.Decode(i); //hello world
```

**When using a new encoder for the first time, the required tiktoken files for the encoder will be downloaded from the internet. This may take some time.** Once the download is successful, subsequent uses will not require downloading again. You can set `TikToken.PBEFileDirectory` before using the encoder to modify the storage path of the downloaded tiktoken files, or you can pre-download the files to avoid network issues causing download failures.

**Why are the tiktoken files not integrated into the package?** On one hand, this would make the package size larger. On the other hand, I want to stay as consistent as possible with OpenAI's official Python code.

If you are deploying cloud functions, such as "Azure App Service," which cannot read/write local files, please package tiktoken files(PBE Dir) with the publish files.

Below are the file download links:
[p50k_base.tiktoken](https://openaipublic.blob.core.windows.net/encodings/p50k_base.tiktoken)
[cl100k_base.tiktoken](https://openaipublic.blob.core.windows.net/encodings/cl100k_base.tiktoken)
[o200k_base.tiktoken](https://openaipublic.blob.core.windows.net/encodings/o200k_base.tiktoken)

## Benchmark Test

Measured on November 18, 2026, using BenchmarkDotNet 0.15.6, Windows 11 x64 (14 physical cores, 20 logical processors), and .NET SDK 10.0.403. All runs used Release builds, one launch, three warmup iterations, and eight measurement iterations. The tables report mean elapsed time and managed bytes allocated per operation.

### Encode and decode

One operation encodes and decodes the same English paragraph 10,000 times using an initialized `cl100k_base` encoder. Both libraries process the same text. [Benchmark source](TiktokenSharp.Benchmark/BenchmarkTest.cs)

| Runtime | Library | Mean per 10,000 round trips | Allocated per 10,000 round trips |
| --- | --- | ---: | ---: |
| .NET 10.0.14 | SharpToken 2.0.6 | 55.94 ms | 22.13 MiB |
| .NET 10.0.14 | TiktokenSharp | 37.61 ms | 11.67 MiB |
| .NET 8.0.33 | SharpToken 2.0.6 | 65.59 ms | 22.13 MiB |
| .NET 8.0.33 | TiktokenSharp | 39.12 ms | 11.67 MiB |

### Individual operations

Both builds ran the same four benchmarks on .NET 10.0.14 with a live `cl100k_base` encoder. Each row measures one call. The long-piece input is a single run of 8,192 ASCII `a` characters. [Baseline commit 779cf1b](https://github.com/aiqinxuancai/TiktokenSharp/commit/779cf1b0258ee20a8b6fba6bc2492355e740c89f) · [Benchmark source](TiktokenSharp.Benchmark/TokenizerBenchmarks.cs)

| Operation | Baseline mean | Optimized mean | Baseline allocation | Optimized allocation |
| --- | ---: | ---: | ---: | ---: |
| Get an already loaded encoding | 46.59 ms | 5.57 µs | 36,340,341 B | 1,328 B |
| Count one English sentence | 629.6 ns | 589.3 ns | 128 B | 0 B |
| Count one 8,192-byte piece | 50.96 ms | 1.18 ms | 65,728 B | 0 B |
| Decode the tokens for 256 copies of the sentence | 24.70 µs | 18.00 µs | 102,560 B | 32,792 B |

### First vocabulary load

A separate fresh-process measurement loaded `o200k_base` from a local vocabulary file on .NET 10.0.14 with tiered compilation disabled. The first `GetEncoding` call allocated 71.51 MiB on the baseline and 20.85 MiB on the optimized build. With the encoder still alive, the increase in managed heap size after a full GC was 17.77 MiB and 9.17 MiB, respectively.

### Reproduce

Run the BenchmarkDotNet workloads from the repository root with the .NET 10 SDK and both .NET 8 and .NET 10 runtimes installed.

```bash
dotnet run --project TiktokenSharp.Benchmark -f net10.0 -c Release -- --filter "*" --launchCount 1 --warmupCount 3 --iterationCount 8
```

Use `--list flat` to list the benchmarks, or replace `"*"` with `"*TokenizerBenchmarks*"` to run only the individual operations.

To reproduce the baseline column, check out `779cf1b` in a separate worktree, copy the same `TokenizerBenchmarks.cs` into its benchmark project, and run the individual operations there.


## Update

### 1.2.2 20261118

* Use `o200k_base` for local token count estimates for GPT-6.x models.
* Stream vocabulary files and reuse loaded vocabularies and encoders across factory calls. On .NET 10, loading `o200k_base` from disk allocated 20.85 MiB, down from 71.51 MiB.
* Speed up encoding and counting long pieces on .NET 7 and later. In the .NET 10 benchmark, counting 8,192 consecutive ASCII `a` characters took 1.18 ms, down from 50.96 ms. [Benchmark results](#individual-operations)
* Encode `ReadOnlySpan<char>` inputs directly and reuse buffers when counting tokens and decoding text to reduce temporary allocations.

### 1.2.1 20260222
* Fix cache write failures in read-only environments by falling back from `PBEFileDirectory`/`BaseDirectory` to system `Temp`.
* Add in-memory loading fallback when both cache directories are unavailable to avoid runtime crashes.
* Upgrade `TiktokenSharp.Test` target framework to `.NET 10`.
* Move benchmark code out of `TiktokenSharp.Test` into dedicated project `TiktokenSharp.Benchmark` (`net8.0` + `net10.0`).
* Remove `--benchmark` mode from test runner and keep `TiktokenSharp.Test` focused on functional validation.

### 1.2.0 20251119
* Optimize memory allocation and execution efficiency.

### 1.1.8 20250814
* Add Support gpt-5 models.

### 1.1.7 20250314
* Add Support o3 models.

### 1.1.6 20241224
* Optimize model name matching encoding.

### 1.1.5 20240913
* Add Support o1 models(o200k_base).

### 1.1.4 20240514
* Add Support gpt-4o(o200k_base).

### 1.1.0 20240408
* **Optimize algorithm efficiency**.

### 1.0.9 20240208
* Adding support for new OpenAI embeddings. by @winzig

### 1.0.7 20231010
* Corrected the issue where some new models could not properly obtain the encoder.

### 1.0.7 20231010
* Corrected the issue where some new models could not properly obtain the encoder.

### 1.0.6 20230625
* Replace WebClient with HttpClient, add async methods.

### 1.0.5 20230508
* New support for .Net Standard 2.0 has been added, making TiktokenSharp usable in the .Net Framework.

### 1.0.4 20230424
* Add method TikToken.GetEncoding(encodingName).

### 1.0.3 20230321
* **GetEncodingSetting** now supports the model of **gpt-4** and also allows for encoding names to be directly passed in.

### 1.0.2 20230317
* add method **TikToken.PBEFileDirectory** to allow for custom storage directory of bpe files. the path needs to be set before **TikToken.EncodingForModel()**.

### 1.0.1 20230313
* p50k_base encoding algorithm that supports the text-davinci-003 model.


