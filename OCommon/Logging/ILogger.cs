using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Logging
{
    /// <summary>
    /// 日志接口
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        ///表示是否启用调试日志级别
        /// </summary>
        bool IsDebugEnabled { get; }
        /// <summary>
        /// 记录调试级别日志
        /// </summary>
        /// <param name="message"></param>
        void Debug(object message);
        /// <summary>
        /// 记录调试级别日志
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        void DebugFormat(string format, params object[] args);
        /// <summary>
        /// 记录调试级别日志
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        void Debug(object message, Exception ex);
        /// <summary>
        /// 记录一般信息级别日志
        /// </summary>
        /// <param name="message"></param>
        void Info(object message);
        /// <summary>
        /// 记录一般信息级别日志
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        void InfoFormat(string format, params object[] args);
        /// <summary>
        /// 记录一般信息级别日志
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        void Info(object message, Exception ex);
        /// <summary>
        /// 记录错误级别日志
        /// </summary>
        /// <param name="message"></param>
        void Error(object message);
        /// <summary>
        /// 记录错误级别日志
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        void ErrorFormat(string format, params object[] args);
        /// <summary>
        /// 记录错误级别日志
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        void Error(object message, Exception ex);
        /// <summary>
        /// 记录警告级别日志
        /// </summary>
        /// <param name="message"></param>
        void Warn(object message);
        /// <summary>
        /// 记录警告级别日志
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        void WarnFormat(string format, params object[] args);
        /// <summary>
        /// 记录警告级别日志
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        void Warn(object message, Exception ex);
        /// <summary>
        /// 记录致命级别日志
        /// </summary>
        /// <param name="message"></param>
        void Fatal(object message);
        /// <summary>
        /// 记录致命级别日志
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        void FatalFormat(string format, params object[] args);
        /// <summary>
        /// 记录致命级别日志
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        void Fatal(object message, Exception ex);
    }
}
