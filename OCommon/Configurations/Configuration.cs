using OceanChip.Common.Components;
using OceanChip.Common.Logging;
using OceanChip.Common.Scheduling;
using OceanChip.Common.Serializing;
using OceanChip.Common.Socketing.Framing;
using OceanChip.Common.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Configurations
{
    public class Configuration
    {
        public static Configuration Instance { get; private set; }

        private Configuration() { }
        public static Configuration Create()
        {
            Instance = new Configuration();
            return Instance;
        }
        public Configuration SetDefault<TService,TImplementer>(string serviceName=null,LifeStyle life= LifeStyle.Singleton)
            where TService:class
            where TImplementer : class, TService
        {
            ObjectContainer.Register<TService, TImplementer>(serviceName, life);
            return this;
        }
        public Configuration SetDefault<TService, TImplementer>(TImplementer instance, string serviceName=null)
            where TService : class
            where TImplementer : class, TService
        {
            ObjectContainer.Register<TService, TImplementer>(instance, serviceName);
            return this;
        }
        public Configuration RegisterCommonComponents()
        {
            SetDefault<ILoggerFactory, EmptyLoggerFactory>();
            SetDefault<IBinarySerializer, DefaultBinarySerializer>();
            SetDefault<IJsonSerializer, NotImplementedJsonSerializer>();
            SetDefault<IScheduleService, ScheduleService>(null, LifeStyle.Transient);
            SetDefault<IMessageFramer, LengthPrefixMessageFramer>(null, LifeStyle.Transient);
            SetDefault<IPerformanceService, DefaultPerformanceService>(null, LifeStyle.Transient);
            
            return this;
        }
        public Configuration RegisterUnhandledExceptionHandler()
        {
            var logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
            AppDomain.CurrentDomain.UnhandledException += (sender, e) => logger.Error($"Unhandled Exception:{e.ExceptionObject}。");
            return this;
        }
    }
}
