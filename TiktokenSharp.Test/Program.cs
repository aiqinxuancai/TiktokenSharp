using System.Diagnostics;
using TiktokenSharp;
using System;
using TiktokenSharp.Test.Utils;
using BenchmarkDotNet.Running;

namespace TiktokenSharp.Test 
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //TikToken.PBEFileDirectory = @"D:\tikTokenFiles";
            GPT4o();
            //GPT35();
            //TextDavinci();
            var b = new BenchmarkTest();
            
            b.TiktokenSharp();


            var summary = BenchmarkRunner.Run<BenchmarkTest>();

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

            var c = tikToken.Encode("hello <|endoftext|>");
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

            var c = tikToken.Encode("hello <|endoftext|>");
            Debug.Assert(c.IsEqualTo(new List<int>() { 31373, 220, 50256 }));

            var t1 = tikToken.Encode("我很抱歉，我不能提供任何非法或不道德的建议。快速赚钱是不容易的，需要耐心、刻苦努力和经验。如果您想增加收入，请考虑增加工作时间、寻找其他业务机会、学习新技能或提高自己的价值等方法。请记住，通过合法而道德的方式来获得收入，才是长期稳定的解决方案。");

            Debug.Assert(t1.Count == 257);

            Console.WriteLine(t1.Count); //257
        }
    }
}



