using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Utilities
{
    public static class ObjectUtils
    {
        /// <summary>
        /// 创建对象实例
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static T CreateObject<T>(object source)where T : class,new()
        {
            var obj = new T();
            var psFromSource = source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            foreach(var p in properties)
            {
                var sourceProperty = psFromSource.FirstOrDefault(ps => ps.Name == p.Name);
                if (sourceProperty != null)
                    p.SetValue(obj, sourceProperty.GetValue(source, null), null);
            }
            return obj;
        }
        public static void UpdateObject<TTarget,TSource>(TTarget target,TSource source,params Expression<Func<TSource,object>>[] propertyExpressionsFromSource)
            where TTarget:class where TSource:class
        {
            Check.NotNull(target, nameof(target));
            Check.NotNull(source, nameof(source));
            Check.NotNull(propertyExpressionsFromSource, nameof(propertyExpressionsFromSource));

            var properties = target.GetType().GetProperties();

            foreach(var pExpression in propertyExpressionsFromSource)
            {
                var propertyFromSource = GetProperty<TSource, object>(pExpression);
                var propertyFromTarget = properties.SingleOrDefault(p => p.Name == propertyFromSource.Name);
                if (propertyFromTarget != null)
                    propertyFromTarget.SetValue(target, propertyFromSource.GetValue(source, null), null);
            }
        }

        private static PropertyInfo GetProperty<TSource, TProperty>(Expression<Func<TSource, TProperty>> pExpression) where TSource : class
        {
            var type = typeof(TSource);
            MemberExpression expression = null;

            switch (pExpression.NodeType)
            {
                case ExpressionType.Convert:
                    expression = ((UnaryExpression)pExpression.Body).Operand as MemberExpression;
                    break;
                case ExpressionType.MemberAccess:
                    expression = pExpression.Body as MemberExpression;
                    break;
            }

            if (expression == null)
                throw new ArgumentException($"Lambda表达式无效：{pExpression.ToString()}");

            var propInfo = expression.Member as PropertyInfo;
            if (propInfo == null)
                throw new ArgumentException($"表达式：{pExpression.ToString()} 设置的为字段而非属性。");

            if (type != propInfo.ReflectedType && !type.IsSubclassOf(propInfo.ReflectedType))
                throw new ArgumentException($"表达式：{pExpression.ToString()}无法将类型从{propInfo.ReflectedType.Name}转换为{type.Name}.");

            return propInfo;
        }
    }
}
