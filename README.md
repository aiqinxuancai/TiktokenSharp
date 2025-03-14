# TiktokenSharp

This library is a C# implementation of the token count calculation, referencing OpenAI's official Rust language version. Currently, the encoding algorithms for `o200k_base`, `cl100k_base`, and `p50k_base` have been implemented. You can directly obtain the corresponding encoding algorithm using the model name.

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

I noticed that some users would like to get a comparison of efficiency. Here, I use SharpToken as the basic comparison, with the encoder cl100k_base, on the .Net 6.0 in Debug mode.
* TiktokenSharp Version: 1.1.0 
* SharpToken Version: 2.0.1

<details> 
<summary>Code：</summary>

```csharp
private GptEncoding _sharpToken = GptEncoding.GetEncoding("cl100k_base");
private TikToken _tikToken = TikToken.GetEncoding("cl100k_base");

private string _kLongText = "King Lear, one of Shakespeare's darkest and most savage plays, tells the story of the foolish and Job-like Lear, who divides his kingdom, as he does his affections, according to vanity and whim. Lear’s failure as a father engulfs himself and his world in turmoil and tragedy.";

[Benchmark]
public int SharpToken()
{
    var sum = 0;
    for (var i = 0; i < 10000; i++)
    {
        var encoded = _sharpToken.Encode(_kLongText);
        var decoded = _sharpToken.Decode(encoded);
        sum += decoded.Length;
    }

    return sum;
}

[Benchmark]
public int TiktokenSharp()
{
    var sum = 0;
    for (var i = 0; i < 10000; i++)
    {
        var encoded = _tikToken.Encode(_kLongText);
        var decoded = _tikToken.Decode(encoded);
        sum += decoded.Length;
    }

    return sum;
}
```
  
</details>


|        Method |      Job |  Runtime |      Mean |    Error |   StdDev |      Gen0 |  Allocated |
|-------------- |--------- |--------- |----------:|---------:|---------:|----------:|-----------:|
| TiktokenSharp | .NET 8.0 | .NET 8.0 |  98.34 ms | 0.198 ms | 0.176 ms | 9833.3333 | 82321080 B |
|    SharpToken | .NET 8.0 | .NET 8.0 | 116.38 ms | 1.026 ms | 0.909 ms | 2000.0000 | 23201696 B |


## Update

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


