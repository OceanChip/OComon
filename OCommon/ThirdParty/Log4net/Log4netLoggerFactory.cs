using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Layout;
using OceanChip.Common.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Log4net
{
    public class Log4netLoggerFactory : ILoggerFactory
    {
        public Log4netLoggerFactory(string configFile)
        {
            var file = new FileInfo(configFile);
            if (!file.Exists)
            {
                file = new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configFile));
            }
            if (file.Exists)
            {
                XmlConfigurator.ConfigureAndWatch(file);
            }
            else
            {
                BasicConfigurator.Configure(new ConsoleAppender { Layout = new PatternLayout() });
            }
        }
        public ILogger Create(string name)
        {
            return new Log4netLogger(LogManager.GetLogger(name));
        }

        public ILogger Create(Type type)
        {
            return new Log4netLogger(LogManager.GetLogger(type));
        }
    }
}
