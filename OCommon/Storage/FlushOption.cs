using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Storage
{
    public enum FlushOption
    {
        /// <summary>
        /// 将数据刷到操作系统的缓存
        /// </summary>
        FlushToOS,
        /// <summary>
        /// 将数据写到磁盘
        /// </summary>
        FlushToDisk,
    }
}
