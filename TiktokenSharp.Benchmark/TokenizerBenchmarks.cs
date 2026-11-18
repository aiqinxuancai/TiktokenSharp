using BenchmarkDotNet.Attributes;

namespace TiktokenSharp.Benchmark;

[MemoryDiagnoser]
public class TokenizerBenchmarks
{
    private const string Sentence = "The tokenizer counts words, punctuation, and numbers like 12345.";
    private readonly string _longPiece = new string('a', 8192);
    private TikToken _tokenizer = null!;
    private List<int> _tokens = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tokenizer = TikToken.GetEncoding("cl100k_base");
        _tokens = _tokenizer.Encode(string.Concat(Enumerable.Repeat(Sentence, 256)));
    }

    [Benchmark]
    public TikToken GetLoadedEncoding() => TikToken.GetEncoding("cl100k_base");

    [Benchmark]
    public int CountSentence() => _tokenizer.CountTokens(Sentence);

    [Benchmark]
    public int CountLongPiece() => _tokenizer.CountTokens(_longPiece);

    [Benchmark]
    public string Decode() => _tokenizer.Decode(_tokens);
}
