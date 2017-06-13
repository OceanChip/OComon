using OceanChip.Common.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OceanChip.Common.Extensions
{
    public static class TaskSchedulerExtensions
    {
        public static Task StartDelayedTask(this TaskFactory factory,int milisecondsDelay,Action action)
        {
            Check.NotNull(factory, nameof(factory));
            Check.Nonnegative(milisecondsDelay, nameof(milisecondsDelay));
            Check.NotNull(action, nameof(action));

            if (factory.CancellationToken.IsCancellationRequested)
            {
                return new Task(() => { }, factory.CancellationToken);
            }

            var tcs = new TaskCompletionSource<object>(factory.CreationOptions);
            var ctr = default(CancellationTokenRegistration);
            var timer = new Timer(self =>
            {
                ctr.Dispose();
                ((Timer)self).Dispose();
                tcs.TrySetResult(null);
            });

            try
            {
                timer.Change(milisecondsDelay, Timeout.Infinite);
            }
            catch (ObjectDisposedException) { }//当用户取消会发生此异常

            return tcs.Task.ContinueWith(_ => action(), factory.CancellationToken,
                TaskContinuationOptions.OnlyOnRanToCompletion,factory.Scheduler??TaskScheduler.Current);
        }
    }
}
