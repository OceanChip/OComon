using Autofac;
using OceanChip.Common.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Autoface
{
    public class AutofacObjectContainer : IObjectContainer
    {
        private readonly IContainer _container;
        public AutofacObjectContainer()
        {
            _container = new ContainerBuilder().Build();
        }
        public AutofacObjectContainer(ContainerBuilder containerBuilder)
        {
            _container = containerBuilder.Build();
        }
        public IContainer Container
        {
            get
            {
                return _container;
            }
        }
        public void RegisterType(Type implementationType, string serviceName = null, LifeStyle life = LifeStyle.Singleton)
        {
            var builder =new  ContainerBuilder();
            var registrationBuilder = builder.RegisterType(implementationType);
            if (!string.IsNullOrEmpty(serviceName))
            {
                registrationBuilder.Named(serviceName, implementationType);
            }
            if (life == LifeStyle.Singleton)
                registrationBuilder.SingleInstance();
            builder.Update(_container);
        }
        public void RegisterType(Type serviceType, Type implementationType, string serviceName = null, LifeStyle life = LifeStyle.Singleton)
        {
            var builder = new ContainerBuilder();
            var registrationBuilder = builder.RegisterType(implementationType).As(serviceType); ;
            if (!string.IsNullOrEmpty(serviceName))
            {
                registrationBuilder.Named(serviceName, implementationType);
            }
            if (life == LifeStyle.Singleton)
                registrationBuilder.SingleInstance();
            builder.Update(_container);
        }

        public TService Resolve<TService>() where TService : class
        {
           return  _container.Resolve<TService>();
        }

        public object Resolve(Type serviceType)
        {
            return _container.Resolve(serviceType);
        }

        public TService ResolveNamed<TService>(string serviceName) where TService : class
        {
            return _container.ResolveNamed<TService>(serviceName);
        }

        public object ResolveNamed(string serviceName, Type serviceType)
        {
            return _container.ResolveNamed(serviceName, serviceType);
        }

        public bool TryResolve<TService>(out TService instance) where TService : class
        {
            return _container.TryResolve<TService>(out instance);
        }

        public bool TryResolve(Type serviceType, out object instance)
        {
            return _container.TryResolve(serviceType, out instance);
        }

        public bool TryResolveNamed(string serviceName, Type serviceType, out object instance)
        {
           return _container.TryResolveNamed(serviceName, serviceType, out instance);
        }

        public void Register<TService, TImplementer>(string serviceName, LifeStyle life)
            where TService : class
            where TImplementer : class, TService
        {
            var builder = new ContainerBuilder();
            var registrationBuilder = builder.RegisterType<TImplementer>().As<TService>();
            if (!string.IsNullOrEmpty(serviceName))
            {
                registrationBuilder.Named<TService>(serviceName);
            }
            if (life == LifeStyle.Singleton)
                registrationBuilder.SingleInstance();

            builder.Update(_container);
        }

        public void RegisterInstance<TService, TImplementer>(TImplementer instance, string serviceName)
            where TService:class
            where TImplementer:class,TService
        {
            var builder = new ContainerBuilder();
            var registerationBuilder = builder.RegisterInstance(instance).As<TService>().SingleInstance();
            if (!string.IsNullOrEmpty(serviceName))
                registerationBuilder.Named<TService>(serviceName);
            builder.Update(_container);
        }
    }
}
