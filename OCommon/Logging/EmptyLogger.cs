using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Logging
{
    public class EmptyLogger : ILogger
    {
        public bool IsDebugEnabled { get{ return false; } }

        public void Debug(object message)
        {
        }

        public void Debug(object message, Exception ex)
        {
        }

        public void DebugFormat(string format, params object[] args)
        {
        }

        public void Error(object message)
        {
        }

        public void Error(object message, Exception ex)
        {
        }

        public void ErrorFormat(string format, params object[] args)
        {
        }

        public void Fatal(object message)
        {
        }

        public void Fatal(object message, Exception ex)
        {
        }

        public void FatalFormat(string format, params object[] args)
        {
        }

        public void Info(object message)
        {
        }

        public void Info(object message, Exception ex)
        {
        }

        public void InfoFormat(string format, params object[] args)
        {
        }

        public void Warn(object message)
        {
        }

        public void Warn(object message, Exception ex)
        {
        }

        public void WarnFormat(string format, params object[] args)
        {
        }
    }
}
