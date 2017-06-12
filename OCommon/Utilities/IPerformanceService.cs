using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Utilities
{
    public interface IPerformanceService
    {
        string Name { get;  }
        PerformanceServiceSetting Settings { get; }

        void Start();
        void Stop();
        void IncrementKeyCount(string key, double rtMilliseconds);
        void UpdateKeyCount(string key, long count, double rtMilliseconds);
        PerformanceInfo GetKeyPerformanceInfo(string key);
        IPerformanceService Initialize(string name, PerformanceServiceSetting setting = null);
    }
    public class PerformanceServiceSetting
    {
        public int StatIntervalSeconds { get; set; }
        public bool AutoLogging { get; set; }
        public Func<string> GetLogContextTextFunc { get; set; }
        public Action<PerformanceInfo> PerformanceInfoHandler { get; set; }
    }
    public class PerformanceInfo
    {
        public long TotalCount { get; private set; }
        public long Throughput { get; private set; }
        public long AverageThroughput { get; private set; }
        public double RT { get;private set; }
        public double AverageRT { get; private set; }
        public PerformanceInfo(long totalCount,long throughput,long averageThroughput,double rt,double averageRT)
        {
            this.TotalCount = totalCount;
            this.Throughput = throughput;
            this.AverageThroughput = averageThroughput;
            this.RT = rt;
            this.AverageRT = averageRT;
        }
    }
}
