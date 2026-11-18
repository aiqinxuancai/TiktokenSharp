using BenchmarkDotNet.Running;

namespace TiktokenSharp.Benchmark;

internal class Program
{
    static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
