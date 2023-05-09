using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace TiktokenSharp.Utils
{
    internal class ResourceHelper
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="">cl100k_base.tiktoken</param>
        /// <returns></returns>
        public static string GetTiktokenLocalResource(string name) 
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Stream resourceStream = assembly.GetManifestResourceStream($"TiktokenSharp.TiktokenFiles.{name}");

            if (resourceStream != null)
            {
                StreamReader reader = new StreamReader(resourceStream, Encoding.UTF8);
                string text = reader.ReadToEnd();
                return text;
            }
            return string.Empty;
        }

        public static string[] GetTiktokenLocalResourceLines(string name)
        {
            var str = GetTiktokenLocalResource(name);

            if (!string.IsNullOrEmpty(name))
            {
                return str.Split('\n');
            }
            return new string[0];
        }
    }
}
