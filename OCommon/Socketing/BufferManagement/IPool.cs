using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Socketing.BufferManagement
{
    public interface IPool
    {
        /// <summary>
        /// 总大小
        /// </summary>
        int TotalCount { get;  }
        /// <summary>
        /// 有效
        /// </summary>
        int AvailableCount { get; }
        /// <summary>
        /// 收缩
        /// </summary>
        /// <returns></returns>
        bool Shrink();

    }
    public interface IPool<T> : IPool
    {
        T Get();
        void Return(T item);
    }
}
