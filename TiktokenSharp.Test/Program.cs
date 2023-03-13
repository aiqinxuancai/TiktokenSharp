// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using TiktokenSharp;

Console.WriteLine("Hello, World!");


TikToken tikToken = TikToken.EncodingForModel("gpt-3.5-turbo");

var i = tikToken.Encode("hello world");
var d = tikToken.Decode(i);


Debug.Assert(i.IsEqualTo( new List<int>() { 15339, 1917 }));
Debug.Assert(tikToken.Decode(new List<int>() { 15339, 1917 }) == "hello world");

var c = tikToken.Encode("hello <|endoftext|>", allowedSpecial: "all");
Debug.Assert(c.IsEqualTo(new List<int>() { 15339, 220, 100257 }));

var t1 = tikToken.Encode("我很抱歉，我不能提供任何非法或不道德的建议。快速赚钱是不容易的，需要耐心、刻苦努力和经验。如果您想增加收入，请考虑增加工作时间、寻找其他业务机会、学习新技能或提高自己的价值等方法。请记住，通过合法而道德的方式来获得收入，才是长期稳定的解决方案。");

Debug.Assert(t1.Count == 135);

Console.WriteLine(t1.Count); //35

Console.WriteLine("test clear");

///


TikToken tikTokenTextDavinci003 = TikToken.EncodingForModel("text-davinci-003");

var i2 = tikTokenTextDavinci003.Encode("hello world");
var d2 = tikTokenTextDavinci003.Decode(i2);

Debug.Assert(i2.IsEqualTo(new List<int>() { 31373, 995 }));
Debug.Assert(tikTokenTextDavinci003.Decode(new List<int>() { 31373, 995 }) == "hello world");

var c2 = tikTokenTextDavinci003.Encode("hello <|endoftext|>", allowedSpecial: "all");
Debug.Assert(c2.IsEqualTo(new List<int>() { 31373, 220, 50256 }));

var t2 = tikTokenTextDavinci003.Encode("我很抱歉，我不能提供任何非法或不道德的建议。快速赚钱是不容易的，需要耐心、刻苦努力和经验。如果您想增加收入，请考虑增加工作时间、寻找其他业务机会、学习新技能或提高自己的价值等方法。请记住，通过合法而道德的方式来获得收入，才是长期稳定的解决方案。");

Debug.Assert(t2.Count == 257);

Console.WriteLine(t2.Count); //257

Console.WriteLine("test clear");


public static class ListExtensions
{
    public static bool IsEqualTo(this List<int> list1, List<int> list2)
    {
        if (list1.Count != list2.Count) return false;
        for (int i = 0; i < list1.Count; i++)
        {
            if (list1[i] != list2[i]) return false;
        }
        return true;
    }
}