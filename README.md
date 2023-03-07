# TiktokenSharp

Due to the lack of a C# version of `cl100k_base` encoding (gpt-3.5-turbo), I have implemented a basic solution with encoding and decoding methods based on the official Rust implementation.

Currently, only `cl100k_base` has been implemented. Other encodings will be added in future submissions. If you encounter any issues or have questions, please feel free to submit them on the `lssues`."

### Start

```csharp
using TiktokenSharp;
TikToken tikToken = TikToken.EncodingForModel("gpt-3.5-turbo");
var i = tikToken.Encode("hello world"); //[15339, 1917]
var d = tikToken.Decode(i); //hello world
```
