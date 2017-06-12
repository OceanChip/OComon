using OceanChip.Common.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OceanChip.Common.Scheduling
{
    public class ScheduleService : IScheduleService
    {
        private readonly object _lockObj = new object();

        private readonly Dictionary<string, TimerBaseTask> _taskDict = new Dictionary<string, TimerBaseTask>();
        private readonly ILogger _logger;

        public ScheduleService(ILoggerFactory logFactory)
        {
            _logger = logFactory.Create(GetType().FullName);
        }
        public void StartTask(string name, Action action, int dueTime, int period)
        {
            lock (_lockObj)
            {
                if (_taskDict.ContainsKey(name)) return;
                var timer = new Timer(TaskCallback, name, Timeout.Infinite, Timeout.Infinite);
                _taskDict.Add(name, new TimerBaseTask { Name = name, Action = action, Timer = timer, DueTime = dueTime, Period = period, Stopped = false });
                timer.Change(dueTime, period);
            }
        }

        public void StopTask(string name)
        {
            lock (_lockObj)
            {
                if (_taskDict.ContainsKey(name))
                {
                    var task = _taskDict[name];
                    task.Stopped = true;
                    task.Timer.Dispose();
                    _taskDict.Remove(name);
                }
            }
        }
        private void TaskCallback(object obj)
        {
            var taskName = (string)obj;
            TimerBaseTask task;

            if(_taskDict.TryGetValue(taskName,out task))
            {
                try
                {
                    if (!task.Stopped)
                    {
                        task.Timer.Change(Timeout.Infinite, Timeout.Infinite);
                        task.Action();
                    }
                }
                catch (ObjectDisposedException) { }
                catch (Exception ex)
                {
                    if(_logger != null)
                    {
                        _logger.Error($"任务发生异常，name:{task.Name},due:{task.DueTime},period:{task.Period}.",ex);
                    }
                }
                finally
                {
                    try
                    {
                        if (!task.Stopped)
                        {
                            task.Timer.Change(task.Period, task.Period);
                        }
                    }
                    catch (ObjectDisposedException) { }
                    catch(Exception ex)
                    {
                        if (_logger != null)
                        {
                            _logger.Error($"更改定时器发生异常，name:{task.Name},due:{task.DueTime},period:{task.Period}.", ex);
                        }
                    }
                }
            }
        }
        class TimerBaseTask
        {
            public string Name;
            public Action Action;
            public Timer Timer;
            public int DueTime;
            public int Period;
            public bool Stopped;
        }
    }
}
