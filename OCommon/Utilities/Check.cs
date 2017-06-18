using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Utilities
{
    public static class Check
    {
        public static void NotNull<T>(T argument,string argumentName)where T : class
        {
            if (argument == null)
                throw new ArgumentNullException($"{argumentName}不能为空.");
        }
        public static void NotNullOrEmpty(string argument, string argumentName)
        {
            if (string.IsNullOrEmpty(argument))
                throw new ArgumentNullException(argument, argumentName + "不能为NULL或者为空。");
        }
        public static void Positive(int number,string argumentName)
        {
            if (number <= 0)
                throw new ArgumentOutOfRangeException(argumentName, argumentName + "必须为正。");
        }
        public static void Positive(long number,string argumentName)
        {
            if (number <= 0)
                throw new ArgumentOutOfRangeException(argumentName, argumentName + "必须为正。");
        }
        public static void Nonnegative(long number,string argumentName)
        {
            if (number < 0)
                throw new ArgumentOutOfRangeException(argumentName, argumentName + "必须大于等于0。");
        }
        public static void Nonnegative(int number, string argumentName)
        {
            if (number < 0)
                throw new ArgumentOutOfRangeException(argumentName, argumentName + "必须大于等于0。");
        }
        /// <summary>
        /// 判断Guid是否为空，若为空，抛出<see cref="ArgumentException"/>异常
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="argumentName"></param>
        public static void NotEmptyGuid(Guid guid,string argumentName)
        {
            if (Guid.Empty == guid)
                throw new ArgumentException(argumentName, argumentName + "参数值不能为Guid.Empty");
        }
        /// <summary>
        /// 判断两个整数是否相等，若不相等扔出<see cref="ArgumentException"/>异常
        /// </summary>
        /// <param name="expected">期望值</param>
        /// <param name="actual">实际值</param>
        /// <param name="argumentName"></param>
        public static void Equal(int expected,int actual,string argumentName)
        {
            if (expected != actual)
                throw new ArgumentException($"参数{argumentName}期望值：{expected},实际：{actual}");
        }

        /// <summary>
        /// 判断两个整数是否相等，若不相等扔出<see cref="ArgumentException"/>异常
        /// </summary>
        /// <param name="expected">期望值</param>
        /// <param name="actual">实际值</param>
        /// <param name="argumentName"></param>
        public static void Equal(long expected, long actual, string argumentName)
        {
            if (expected != actual)
                throw new ArgumentException($"参数{argumentName}期望值：{expected},实际：{actual}");
        }
        /// <summary>
        /// 判断两个布尔型是否相等，若不相等扔出<see cref="ArgumentException"/>异常
        /// </summary>
        /// <param name="expected">期望值</param>
        /// <param name="actual">实际值</param>
        /// <param name="argumentName"></param>
        public static void Equal(bool expected, bool actual, string argumentName)
        {
            if (expected != actual)
                throw new ArgumentException($"参数{argumentName}期望值：{expected},实际：{actual}");
        }
    }
}
