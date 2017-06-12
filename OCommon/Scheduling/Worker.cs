using OceanChip.Common.Components;
using OceanChip.Common.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OceanChip.Common.Scheduling
{
    public class Worker
    {
        private readonly object _lockObject = new object();
        private readonly string _actionName;
        private readonly Action _action;
        private readonly ILogger _logger;
        private Status _status;

        public string ActionName => _actionName;
        public Worker(string actionName,Action action)
        {
            _actionName = actionName;
            _action = action;
            _status = Status.Initial;
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
        }
        public Worker Start()
        {
            lock (_lockObject)
            {
                if (_status == Status.Running) return this;

                _status = Status.Running;
                new Thread(Loop)
                {
                    Name = $"{_actionName}.Worker",
                    IsBackground = true
                }.Start(this);

                return this;
            }
        }
        public Worker Stop()
        {
            lock (_lockObject)
            {
                if (_status == Status.StopRequested) return this;

                _status = Status.StopRequested;
                return this;
            }
        }
        private void Loop(object data)
        {
            var worker = (Worker)data;

            while(worker._status== Status.Running)
            {
                try
                {
                    _action();
                }
                catch (ThreadAbortException)
                {
                    _logger.InfoFormat($"后台任务发生ThreadAbortException异常，尝试重置，名称：{_actionName}");
                    Thread.ResetAbort();
                    _logger.InfoFormat($"重启后台任务ThreadAbortException异常，名称：{_actionName}");
                }
                catch(Exception ex)
                {
                    _logger.Error($"任务现场发生异常，名称：{_actionName}", ex);
                }
            }
        }
        enum Status
        {
            /// <summary>
            /// 初始化
            /// </summary>
            Initial,
            /// <summary>
            /// 运行
            /// </summary>
            Running,
            /// <summary>
            /// 停止请求
            /// </summary>
            StopRequested
        }
    }
}
