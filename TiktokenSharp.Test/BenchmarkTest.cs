using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using SharpToken;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpToken;

namespace TiktokenSharp.Test
{
    /*
     * 
     
|             Method |      Mean |    Error |   StdDev |   Gen0 |   Gen1 | Allocated |
|------------------- |----------:|---------:|---------:|-------:|-------:|----------:|
| SpeedTiktokenSharp |  37.55 us | 0.536 us | 0.501 us | 4.2725 | 0.1221 |  35.09 KB |
|    SpeedSharpToken | 100.14 us | 1.393 us | 1.303 us | 6.7139 | 0.2441 |  54.93 KB |
     

use RegexOptions.Compiled 

|             Method |      Mean |    Error |   StdDev |   Gen0 |   Gen1 | Allocated |
|------------------- |----------:|---------:|---------:|-------:|-------:|----------:|
| SpeedTiktokenSharp |  31.31 us | 0.452 us | 0.401 us | 4.2725 | 0.1221 |  35.09 KB |
|    SpeedSharpToken | 100.28 us | 1.106 us | 1.034 us | 6.7139 | 0.2441 |  54.93 KB |
     
     */


    //[SimpleJob(RuntimeMoniker.Net60)]
    [SimpleJob(RuntimeMoniker.Net80)]
    [SimpleJob(RuntimeMoniker.Net10_0)]
    //[SimpleJob(RuntimeMoniker.Net471)]
    //[RPlotExporter]
    [MemoryDiagnoser]
    public class BenchmarkTest
    {
        private GptEncoding _sharpToken = GptEncoding.GetEncoding("cl100k_base");
        private TikToken _tikToken = TikToken.GetEncoding("cl100k_base");
        //private ITokenizer _tokenizer;
        private string _kLongText = "King Lear, one of Shakespeare's darkest and most savage plays, tells the story of the foolish and Job-like Lear, who divides his kingdom, as he does his affections, according to vanity and whim. Lear’s failure as a father engulfs himself and his world in turmoil and tragedy.";

        private string _kMinText = "Hello, World!";


        //[GlobalSetup]
        //public async Task Setup()
        //{
        //    _sharpToken = GptEncoding.GetEncoding("cl100k_base");
        //    _tikToken = await TikToken.GetEncodingAsync("cl100k_base").ConfigureAwait(false);
        //    //_tokenizer = await TokenizerBuilder.CreateByModelNameAsync("gpt-4").ConfigureAwait(false);
        //    _kLongText = "King Lear, one of Shakespeare's darkest and most savage plays, tells the story of the foolish and Job-like Lear, who divides his kingdom, as he does his affections, according to vanity and whim. Lear’s failure as a father engulfs himself and his world in turmoil and tragedy.";
        //}



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

        //[Benchmark]
        //public int TokenizerLib()
        //{
        //    var sum = 0;
        //    for (var i = 0; i < 10000; i++)
        //    {
        //        var encoded = _tokenizer.Encode(_kLongText);
        //        var decoded = _tokenizer.Decode(encoded.ToArray());
        //        sum += decoded.Length;
        //    }

        //    return sum;
        //}
    }
}
