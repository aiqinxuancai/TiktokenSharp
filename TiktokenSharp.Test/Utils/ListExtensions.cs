using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TiktokenSharp.Test.Utils
{
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
}
