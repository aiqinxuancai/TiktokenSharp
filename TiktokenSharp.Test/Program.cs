using System.Diagnostics;
using TiktokenSharp;
using System;
using TiktokenSharp.Test.Utils;

namespace TiktokenSharp.Test 
{
    internal class Program
    {
        static void Main()
        {
            //TikToken.PBEFileDirectory = @"D:\tikTokenFiles";

            Debug.Assert(typeof(CoreBPE).GetMethod(nameof(CoreBPE.EncodeNative)) != null);
            GPT4o();
            GPT4();
            GPT35();
            TextDavinci();
            SpanEncodingAndCounting();
            CachedVocabularyAndLongMerges().GetAwaiter().GetResult();

            Console.WriteLine("All tests passed!");

        }

        static void SpanEncodingAndCounting()
        {
            var tokenizer = TikToken.GetEncoding("cl100k_base");
            var text = new string('x', 512) + " " + new string('\u00e9', 1024);
            var padded = "[" + text + "]";
            var span = padded.AsSpan(1, text.Length);
            var expected = tokenizer.Encode(text);

            Debug.Assert(tokenizer.Encode(span).IsEqualTo(expected));
            Debug.Assert(tokenizer.CountTokens(span) == expected.Count);
            Debug.Assert(tokenizer.Decode(expected) == text);
        }

        static async Task CachedVocabularyAndLongMerges()
        {
            var originalDirectory = TikToken.PBEFileDirectory;
            var directory = Directory.CreateTempSubdirectory("tiktokensharp-test-");
            var path = Path.Combine(directory.FullName, "cl100k_base.tiktoken");
            const string vocabulary = "YQ== 7\nYWE= 3\nYWFh 1\n8J8= 10\nmoA= 11\n";
            try
            {
                File.WriteAllText(path, vocabulary);
                TikToken.PBEFileDirectory = directory.FullName;
                var tokenizer = await TikToken.EncodingForModelAsync("gpt-4");
                var input = new string('a', 514);
                var expected = Enumerable.Repeat(1, 171).Append(7).ToList();
                Parallel.For(0, 16, _ =>
                {
                    var other = TikToken.GetEncoding("cl100k_base");
                    Debug.Assert(other.Encode(input).SequenceEqual(expected));
                    Debug.Assert(other.CountTokens(input) == expected.Count);
                });
                Debug.Assert(tokenizer.Decode(new List<int> { 10, 11, -1, 100257 }) == "\U0001f680<|endoftext|>");
                Debug.Assert(tokenizer.Decode(new List<int> { 10 }) == "\ufffd");

                // Keep the original tokenizer alive while replacing the vocabulary with a same-size file.
                var stamp = File.GetLastWriteTimeUtc(path);
                File.WriteAllText(path, vocabulary.Replace("YQ== 7", "YQ== 8"));
                File.SetLastWriteTimeUtc(path, stamp.AddMinutes(1));
                var updated = await TikToken.GetEncodingAsync("cl100k_base");
                Debug.Assert(updated.Encode("a").Single() == 8);
                Debug.Assert(tokenizer.Encode("a").Single() == 7);

                File.WriteAllText(path, "invalid");
                try
                {
                    await TikToken.GetEncodingAsync("cl100k_base");
                    throw new Exception("Expected an invalid vocabulary to be rejected.");
                }
                catch (InvalidOperationException ex) when (ex.InnerException is FormatException) { }

                File.WriteAllText(path, vocabulary);
                var repaired = TikToken.EncodingForModel("gpt-4");
                Debug.Assert(repaired.Encode("a").Single() == 7);
            }
            finally
            {
                TikToken.PBEFileDirectory = originalDirectory;
                File.Delete(path);
                directory.Delete();
            }
        }

        static void GPT4o()
        {
            TikToken tikToken = TikToken.EncodingForModel("gpt-4o-vision");
            //TikToken tikToken = TikToken.GetEncoding("o200k_base");
            var i = tikToken.Encode("hello world");
            var d = tikToken.Decode(i);

            Debug.Assert(i.IsEqualTo(new List<int>() { 24912, 2375 }));
            Debug.Assert(tikToken.Decode(new List<int>() { 24912, 2375 }) == "hello world");

            var c = tikToken.Encode("hello <|endoftext|>", new HashSet<string>() { "<|endoftext|>" });
            Debug.Assert(c.IsEqualTo(new List<int>() { 24912, 220, 199999 }));

            var t1 = tikToken.Encode("我很抱歉，我不能提供任何非法或不道德的建议。快速赚钱是不容易的，需要耐心、刻苦努力和经验。如果您想增加收入，请考虑增加工作时间、寻找其他业务机会、学习新技能或提高自己的价值等方法。请记住，通过合法而道德的方式来获得收入，才是长期稳定的解决方案。");

            Debug.Assert(t1.Count == 78);

            Console.WriteLine(t1.Count);
        }


        static void GPT4()
        {
            TikToken tikToken = TikToken.GetEncoding("cl100k_base");
            var i = tikToken.Encode("hello world");
            var d = tikToken.Decode(i);

            Debug.Assert(i.IsEqualTo(new List<int>() { 15339, 1917 }));
            Debug.Assert(tikToken.Decode(new List<int>() { 15339, 1917 }) == "hello world");

            var c = tikToken.Encode("hello <|endoftext|>", new HashSet<string>() { "<|endoftext|>" });
            Debug.Assert(c.IsEqualTo(new List<int>() { 15339, 220, 100257 }));

            var t1 = tikToken.Encode("我很抱歉，我不能提供任何非法或不道德的建议。快速赚钱是不容易的，需要耐心、刻苦努力和经验。如果您想增加收入，请考虑增加工作时间、寻找其他业务机会、学习新技能或提高自己的价值等方法。请记住，通过合法而道德的方式来获得收入，才是长期稳定的解决方案。");

            Debug.Assert(t1.Count == 135);

            Console.WriteLine(t1.Count); 
        }


        static void GPT35()
        {
            TikToken tikToken = TikToken.EncodingForModel("gpt-3.5-turbo"); // TikToken.GetEncoding("cl100k_base");

            var i = tikToken.Encode("hello world");
            var d = tikToken.Decode(i);

            Debug.Assert(i.IsEqualTo(new List<int>() { 15339, 1917 }));
            Debug.Assert(tikToken.Decode(new List<int>() { 15339, 1917 }) == "hello world");

            var c = tikToken.Encode("hello <|endoftext|>", allowedSpecialMode: SpecialTokensMode.All);
            Debug.Assert(c.IsEqualTo(new List<int>() { 15339, 220, 100257 }));

            var t1 = tikToken.Encode("我很抱歉，我不能提供任何非法或不道德的建议。快速赚钱是不容易的，需要耐心、刻苦努力和经验。如果您想增加收入，请考虑增加工作时间、寻找其他业务机会、学习新技能或提高自己的价值等方法。请记住，通过合法而道德的方式来获得收入，才是长期稳定的解决方案。");

            Debug.Assert(t1.Count == 135);

            Console.WriteLine(t1.Count); //35
        }


        static void TextDavinci()
        {
            TikToken tikToken = TikToken.EncodingForModel("text-davinci-003");

            var i = tikToken.Encode("hello world");
            var d = tikToken.Decode(i);

            Debug.Assert(i.IsEqualTo(new List<int>() { 31373, 995 }));
            Debug.Assert(tikToken.Decode(new List<int>() { 31373, 995 }) == "hello world");

            var c = tikToken.Encode("hello <|endoftext|>", allowedSpecialMode: SpecialTokensMode.All);
            Debug.Assert(c.IsEqualTo(new List<int>() { 31373, 220, 50256 }));

            var t1 = tikToken.Encode("我很抱歉，我不能提供任何非法或不道德的建议。快速赚钱是不容易的，需要耐心、刻苦努力和经验。如果您想增加收入，请考虑增加工作时间、寻找其他业务机会、学习新技能或提高自己的价值等方法。请记住，通过合法而道德的方式来获得收入，才是长期稳定的解决方案。");

            Debug.Assert(t1.Count == 257);

            Console.WriteLine(t1.Count); //257
        }
    }
}



