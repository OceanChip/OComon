using OceanChip.Common.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace OceanChip.Common.Log4net
{
    public class Log4netLogger : ILogger
    {
        private readonly ILog _log;

        public Log4netLogger(ILog log)
        {
            this._log = log;
        }

        public bool IsDebugEnabled => _log.IsDebugEnabled;

        public void Debug(object message)
        {
            _log.Debug(message);
        }

        public void Debug(object message, Exception ex)
        {
            _log.Debug(message, ex);
        }

        public void DebugFormat(string format, params object[] args)
        {
            _log.DebugFormat(format, args);
        }

        public void Error(object message)
        {
            _log.Error(message);
        }

        public void Error(object message, Exception ex)
        {
            _log.Error(message, ex);
        }

        public void ErrorFormat(string format, params object[] args)
        {
            _log.ErrorFormat(format, args);
        }

        public void Fatal(object message)
        {
            _log.Fatal(message);
        }

        public void Fatal(object message, Exception ex)
        {
            _log.Fatal(message, ex);
        }

        public void FatalFormat(string format, params object[] args)
        {
            _log.FatalFormat(format, args);
        }

        public void Info(object message)
        {
            _log.Info(message);
        }

        public void Info(object message, Exception ex)
        {
            _log.Info(message, ex);
        }

        public void InfoFormat(string format, params object[] args)
        {
            _log.InfoFormat(format, args);
        }

        public void Warn(object message)
        {
            _log.Warn(message);
        }

        public void Warn(object message, Exception ex)
        {
            _log.Warn(message, ex);
        }

        public void WarnFormat(string format, params object[] args)
        {
            _log.WarnFormat(format, args);
        }
    }
}
