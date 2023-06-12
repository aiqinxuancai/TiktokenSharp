using BenchmarkDotNet.Attributes;
using SharpToken;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TiktokenSharp.Test
{
    /*
     * 

|             Method |      Mean |    Error |   StdDev |   Gen0 |   Gen1 | Allocated |
|------------------- |----------:|---------:|---------:|-------:|-------:|----------:|
| SpeedTiktokenSharp |  31.31 us | 0.452 us | 0.401 us | 4.2725 | 0.1221 |  35.09 KB |
|    SpeedSharpToken | 100.28 us | 1.106 us | 1.034 us | 6.7139 | 0.2441 |  54.93 KB |

|             Method |      Mean |    Error |   StdDev |   Gen0 |   Gen1 | Allocated |
|------------------- |----------:|---------:|---------:|-------:|-------:|----------:|
| SpeedTiktokenSharp |  28.43 us | 0.562 us | 0.806 us | 2.7161 |      - |   22.3 KB |
|    SpeedSharpToken | 106.84 us | 2.104 us | 2.809 us | 6.7139 | 0.2441 |  54.93 KB |


     
     */





    [MemoryDiagnoser]
    public class BenchmarkTest
    {
        const string kLongText = "King Lear, one of Shakespeare's darkest and most savage plays, tells the story of the foolish and Job-like Lear, who divides his kingdom, as he does his affections, according to vanity and whim. Lear’s failure as a father engulfs himself and his world in turmoil and tragedy.";
        
        TikToken tikToken = TikToken.GetEncoding("cl100k_base");
        GptEncoding encoding = GptEncoding.GetEncoding("cl100k_base");


        [Benchmark]
        public async Task SpeedTiktokenSharp()
        {
            var encoded = tikToken.Encode(kLongText);
            var decoded = tikToken.Decode(encoded);
        }

        [Benchmark]
        public async Task SpeedSharpToken()
        {
            var encoded = encoding.Encode(kLongText);
            var decoded = encoding.Decode(encoded);
        }
    }
}
