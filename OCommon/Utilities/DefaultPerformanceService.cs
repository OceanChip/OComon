using OceanChip.Common.Logging;
using OceanChip.Common.Scheduling;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OceanChip.Common.Utilities
{
    public class DefaultPerformanceService : IPerformanceService
    {
        private string _name;
        private PerformanceServiceSetting _setting;
        private string _taskName;

        private readonly ILogger _logger;
        private readonly IScheduleService _scheduleService;
        private readonly ConcurrentDictionary<string, CountInfo> _countInfoDict;
        public string Name { get { return _name; } }

        public PerformanceServiceSetting Settings => throw new NotImplementedException();

        public DefaultPerformanceService(IScheduleService scheduleService,ILoggerFactory logFactiory)
        {
            _scheduleService = scheduleService;
            _logger = logFactiory.Create(GetType().FullName);
            _countInfoDict = new ConcurrentDictionary<string, CountInfo>();
        }

        public PerformanceInfo GetKeyPerformanceInfo(string key)
        {
            CountInfo countInfo;
            if(_countInfoDict.TryGetValue(key,out countInfo))
            {
                return countInfo.GetCurrentPerformanceInfo();
            }
            return null;
        }

        public void IncrementKeyCount(string key, double rtMilliseconds)
        {
            _countInfoDict.AddOrUpdate(key, x =>
            {
                return new CountInfo(this, 1, rtMilliseconds);
            }, (x, y) =>
            {
                y.IncrementTotalCount(rtMilliseconds);
                return y;
            });
        }

        public IPerformanceService Initialize(string name, PerformanceServiceSetting setting = null)
        {
            Ensure.NotNullOrEmpty(name, nameof(name));

            if(setting == null)
            {
                _setting = new PerformanceServiceSetting()
                {
                    AutoLogging = true,
                    StatIntervalSeconds = 1
                };
            }
            else
            {
                _setting = setting;
            }
            Ensure.Positive(_setting.StatIntervalSeconds, "PerformanceServiceSetting.StatIntervalSeconds");

            _name = name;
            _taskName = name + ".Task";
            return this;
        }

        public void Start()
        {
            if (string.IsNullOrWhiteSpace(_taskName))
            {
                throw new Exception($"{GetType().FullName}请先执行Initialize方法进行初始化");
            }

            _scheduleService.StartTask(_taskName, () =>
            {
                foreach (var entry in _countInfoDict)
                {
                    entry.Value.Calculate();
                }
            }, _setting.StatIntervalSeconds * 1000, _setting.StatIntervalSeconds * 1000);
        }

        public void Stop()
        {
            if (string.IsNullOrEmpty(_taskName))
                return;

            _scheduleService.StopTask(_taskName);
        }

        public void UpdateKeyCount(string key, long count, double rtMilliseconds)
        {
            _countInfoDict.AddOrUpdate(key, x =>
            {
                return new CountInfo(this, count, rtMilliseconds);
            },(x,y)=>
            {
                y.UpdateTotalCount(count, rtMilliseconds);
                return y;
            });
        }

    class CountInfo
    {
        private DefaultPerformanceService _service;

        private long _totalCount;
        private long _previousCount;
        private long _throughput;
        private long _averageThroughput;
        private long _throughputCalculateCount;

        private long _rtCount;
        private long _totalRTTime;
        private long _rtTime;
        private double _rt;
        private double _averateRT;
        private long _rtCalculateCount;

        public long TotalCount
        {
            get { return _totalCount; }
        }
        public long Throughput
        {
            get { return _throughput; }
        }
        public long AverageThroughput
        {
            get { return _averageThroughput; }
        }
        public double RT
        {
            get { return _rt; }
        }
        public double AverageRT
        {
            get { return _averateRT; }
        }

        public CountInfo(DefaultPerformanceService service, long initialCount, double rtMilliseconds)
        {
            _service = service;
            _totalCount = initialCount;
            _rtCount = initialCount;
            Interlocked.Add(ref _rtTime, (long)(rtMilliseconds * 1000));
            Interlocked.Add(ref _totalRTTime, (long)(rtMilliseconds * 1000));
        }
        public void IncrementTotalCount(double rtMilliseconds)
        {
            Interlocked.Increment(ref _totalCount);
            Interlocked.Increment(ref _rtCount);
            Interlocked.Add(ref _rtTime, (long)(rtMilliseconds * 1000));
            Interlocked.Add(ref _totalRTTime, (long)(rtMilliseconds * 1000));
        }
        public void UpdateTotalCount(long count, double rtMilliseconds)
        {
            _totalCount = count;
            _rtCount = count;
            Interlocked.Add(ref _rtTime, (long)(rtMilliseconds * 1000));
            Interlocked.Add(ref _totalRTTime, (long)(rtMilliseconds * 1000));
        }
        public void Calculate()
        {
            CalculateThroughput();
            CalculateRT();

            if (_service._setting.AutoLogging)
            {
                var contextText = string.Empty;
                if (_service._setting.GetLogContextTextFunc != null)
                {
                    contextText = _service._setting.GetLogContextTextFunc();
                }
                if (!string.IsNullOrWhiteSpace(contextText))
                {
                    contextText += ", ";
                }
                _service._logger.InfoFormat("{0}, {1}totalCount: {2}, throughput: {3}, averageThrughput: {4}, rt: {5:F3}ms, averageRT: {6:F3}ms", _service._name, contextText, _totalCount, _throughput, _averageThroughput, _rt, _averateRT);
            }
            if (_service._setting.PerformanceInfoHandler != null)
            {
                try
                {
                    _service._setting.PerformanceInfoHandler(GetCurrentPerformanceInfo());
                }
                catch (Exception ex)
                {
                    _service._logger.Error("PerformanceInfo handler execution has exception.", ex);
                }
            }
        }
        public PerformanceInfo GetCurrentPerformanceInfo()
        {
            return new PerformanceInfo(TotalCount, Throughput, AverageThroughput, RT, AverageRT);
        }

        private void CalculateThroughput()
        {
            var totalCount = _totalCount;
            _throughput = totalCount - _previousCount;
            _previousCount = totalCount;

            if (_throughput > 0)
            {
                _throughputCalculateCount++;
                _averageThroughput = totalCount / _throughputCalculateCount;
            }
        }
        private void CalculateRT()
        {
            var rtCount = _rtCount;
            var rtTime = _rtTime;
            var totalRTTime = _totalRTTime;

            if (rtCount > 0)
            {
                _rt = ((double)rtTime / 1000) / rtCount;
                _rtCalculateCount += rtCount;
                _averateRT = ((double)totalRTTime / 1000) / _rtCalculateCount;
            }

            _rtCount = 0L;
            _rtTime = 0L;
        }
    }

    }
}
