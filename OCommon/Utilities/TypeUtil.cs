using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Utilities
{
    public static class TypeUtil
    {
        public static T ConvertType<T>(object value)
        {
            if (value == null)
                return default(T);

            Type type = typeof(T);
            var typeConverter = TypeDescriptor.GetConverter(type);
            //判断对象是否可以直接转换
            if (typeConverter.CanConvertFrom(value.GetType()))
            {
                return (T)typeConverter.ConvertFrom(value);
            }

            typeConverter = TypeDescriptor.GetConverter(value.GetType());
            if (typeConverter.CanConvertTo(type))
            {
                return (T)typeConverter.ConvertTo(value, type);
            }
            return (T)Convert.ChangeType(value, type);
        }
    }
}
