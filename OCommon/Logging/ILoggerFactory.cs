using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Logging
{
    /// <summary>
    /// 日志工厂类接口
    /// </summary>
    public interface ILoggerFactory
    {
        /// <summary>
        /// 通过日志名称创建工厂类
        /// </summary>
        /// <param name="name">日志名称</param>
        /// <returns></returns>
        ILogger Create(string name);
        /// <summary>
        /// 通过<see cref="Type"/>创建日志类
        /// </summary>
        /// <param name="type">类型</param>
        /// <returns></returns>
        ILogger Create(Type type);
    }
}
