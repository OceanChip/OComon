﻿using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Threading
{
    public static  class AsyncHelper
    {
        public static bool IsAsyncMethod(MethodInfo method)
        {
            return (
                method.ReturnType == typeof(Task)||
                (method.ReturnType.GetTypeInfo().IsGenericType && method.ReturnType.GetTypeInfo().GetGenericTypeDefinition() == typeof(Task<>))
                );
        }
        public static TResult RunSync<TResult>(Func<Task<TResult>> func)
        {
            return AsyncContext.Run(func);
        }
        public static void RunSync(Func<Task> action)
        {
            AsyncContext.Run(action);
        }

    }
}
