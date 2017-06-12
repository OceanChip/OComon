using Autofac;
using OceanChip.Common.Autoface;
using OceanChip.Common.Components;
using OceanChip.Common.Log4net;
using OceanChip.Common.Logging;
using OceanChip.Common.Newtonsoft;
using OceanChip.Common.Serializing;

namespace OceanChip.Common.Configurations
{
    public static class ConfigurationExtensions
    {
        public static Configuration UseAutofac(this Configuration configuration)
        {
            return UseAutofac(configuration, new ContainerBuilder());
        }
        public static Configuration UseAutofac(this Configuration configuration,ContainerBuilder builder)
        {
            ObjectContainer.SetContainer(new AutofacObjectContainer(builder));
            return configuration;
        }
        public static Configuration UserJsonNet(this Configuration configuration)
        {
            configuration.SetDefault<IJsonSerializer, NewtonsoftJsonSerializer>(new NewtonsoftJsonSerializer());
            return configuration;
        }
        public static Configuration UseLog4net(this Configuration configutation)
        {
            return UseLog4net(configutation, "log4net.config");
        }
        public static Configuration UseLog4net(this Configuration configutation,string configFile)
        {
            configutation.SetDefault<ILoggerFactory, Log4netLoggerFactory>(new Log4netLoggerFactory(configFile));
            return configutation;
        }
    }
}
