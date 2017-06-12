using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Logging
{
    /// <summary>
    /// 空日志工厂实现类
    /// </summary>
    public class EmptyLoggerFactory : ILoggerFactory
    {
        private static readonly EmptyLogger Logger = new EmptyLogger();
        public ILogger Create(string name)
        {
            return Logger;
        }

        public ILogger Create(Type type)
        {
            return Logger;
        }
    }
}
