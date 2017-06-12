using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OceanChip.Common.Extensions
{
    public static class TaskExtensions
    {
        public static TResult WaitResult<TResult>(this Task<TResult> task,int timeoutMillis)
        {
            if (task.Wait(timeoutMillis))
            {
                return task.Result;
            }
            return default(TResult);
        }
        public static async Task TimeoutAfter(this Task task,int millisecondsDelay)
        {
            var timeoutCancellationTokenSource = new CancellationTokenSource();
            var completeTask = await Task.WhenAny(task, Task.Delay(millisecondsDelay, timeoutCancellationTokenSource.Token));
            if (completeTask == task)
            {
                timeoutCancellationTokenSource.Cancel();
            }
            else
            {
                throw new TimeoutException("操作超时");
            }
        }
        public static async Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, int millisecondsDelay)
        {
            var timeoutCancellationTokenSource = new CancellationTokenSource();
            var completeTask = await Task.WhenAny(task, Task.Delay(millisecondsDelay, timeoutCancellationTokenSource.Token));
            if (completeTask == task)
            {
                timeoutCancellationTokenSource.Cancel();
                return task.Result;
            }
            else
            {
                throw new TimeoutException("操作超时");
            }
        }
    }
}
