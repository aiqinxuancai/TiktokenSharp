using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using SharpToken;
using TiktokenSharp;

namespace TiktokenSharp.Benchmark;

[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class BenchmarkTest
{
    private readonly GptEncoding _sharpToken = GptEncoding.GetEncoding("cl100k_base");
    private readonly TikToken _tikToken = TikToken.GetEncoding("cl100k_base");
    private readonly string _kLongText = "King Lear, one of Shakespeare's darkest and most savage plays, tells the story of the foolish and Job-like Lear, who divides his kingdom, as he does his affections, according to vanity and whim. Lear's failure as a father engulfs himself and his world in turmoil and tragedy.";

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
}
