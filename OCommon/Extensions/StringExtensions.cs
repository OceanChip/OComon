using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// 返回与平台无关的哈希值
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static int GetStringHashcode(this string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;

            unchecked
            {
                int hash = 29;
                foreach(char c in s)
                {
                    hash = (hash << 5) - hash + c;
                }
                if (hash < 0)
                    hash = Math.Abs(hash);
                return hash;
            }
        }
    }
}
