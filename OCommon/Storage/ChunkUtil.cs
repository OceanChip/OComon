using Microsoft.VisualBasic.Devices;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Storage
{

    public class ChunkUtil
    {
        const uint BYTESPERMB = 1024 * 1024;
        public struct ChunkApplyMemoryInfo
        {
            public ulong PyhsioalMemoryMB;
            public ulong UsedMemoryPercent;
            public ulong UsedMemoryMB;
            public ulong MaxAllowUseMemoryMB;
            public ulong ChunkSizeMB;
            public ulong AvailableMemoryMB;
        }
        /// <summary>
        /// 判断内存是否可以缓存数据块
        /// </summary>
        /// <param name="chunkSize"></param>
        /// <param name="maxUseMemoryPercent"></param>
        /// <param name="applyMemoryInfo"></param>
        /// <returns></returns>
        public static bool IsMemoryEnoughToCacheChunk(ulong chunkSize,uint maxUseMemoryPercent,out ChunkApplyMemoryInfo applyMemoryInfo)
        {
            var computerInfo = new ComputerInfo();
            applyMemoryInfo = new ChunkApplyMemoryInfo()
            {
                PyhsioalMemoryMB = computerInfo.TotalPhysicalMemory / BYTESPERMB,
                AvailableMemoryMB = computerInfo.AvailablePhysicalMemory / BYTESPERMB,
                UsedMemoryMB=(computerInfo.TotalPhysicalMemory-computerInfo.AvailablePhysicalMemory)/BYTESPERMB,
                ChunkSizeMB=chunkSize/BYTESPERMB,
               
            };
            applyMemoryInfo.UsedMemoryPercent = applyMemoryInfo.UsedMemoryMB * maxUseMemoryPercent / 100;
            applyMemoryInfo.MaxAllowUseMemoryMB = applyMemoryInfo.PyhsioalMemoryMB * maxUseMemoryPercent / 100;
            return applyMemoryInfo.UsedMemoryMB + applyMemoryInfo.ChunkSizeMB <= applyMemoryInfo.MaxAllowUseMemoryMB;
        }
        /// <summary>
        /// 判断内存是否可以缓存数据块
        /// </summary>
        /// <param name="chunkSize"></param>
        /// <param name="maxUseMemoryPercent"></param>
        /// <returns></returns>
        public static bool IsMemoryEnoughToCacheChunk(ulong chunkSize,uint maxUseMemoryPercent)
        {
            var computerInfo = new ComputerInfo();
            var maxAllowUseMemory = computerInfo.TotalPhysicalMemory * maxUseMemoryPercent / 100;
            var currentUsedMemory = computerInfo.TotalPhysicalMemory - computerInfo.AvailablePhysicalMemory;

            return currentUsedMemory + chunkSize <= maxUseMemoryPercent;
        }
        /// <summary>
        /// 获取当前使用的物理内存百分比
        /// </summary>
        /// <returns></returns>
        public static ulong GetUserMemoryPercent()
        {
            var computeInfo = new ComputerInfo();
            var usedPyhsicalMemory = computeInfo.TotalPhysicalMemory - computeInfo.AvailablePhysicalMemory;
            var usedMemoryPercent = usedPyhsicalMemory * 100 / computeInfo.TotalPhysicalMemory;
            return usedMemoryPercent;
        }
    }
}
