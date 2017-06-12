using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Utilities
{
    /// <summary>
    /// 限速控制
    /// </summary>
    public sealed  class FlowControlUtil
    {
        /// <summary>
        /// 计算限速等待时间
        /// </summary>
        /// <param name="pendingCount"></param>
        /// <param name="thresholdCount"></param>
        /// <param name="stepPercent"></param>
        /// <param name="baseBaseMilliseconds"></param>
        /// <param name="maxWaitMilliseconds"></param>
        /// <returns></returns>
        public static int CalculateFlowControlTimeMilliseconds(int pendingCount,int thresholdCount,int stepPercent,int baseBaseMilliseconds, int maxWaitMilliseconds = 10000)
        {
            var exceedCount = pendingCount - thresholdCount;
            exceedCount = exceedCount <= 0 ? 1 : exceedCount;

            var stepCount = stepPercent * thresholdCount / 100;
            stepCount = stepCount <= 0 ? 1 : stepCount;

            var times = exceedCount / stepCount;
            times = times <= 0 ? 1 : times;

            var waitMilliseconds = times * baseBaseMilliseconds;
            if (waitMilliseconds > maxWaitMilliseconds)
            {
                return maxWaitMilliseconds;
            }
            return waitMilliseconds;
        }
    }
}
