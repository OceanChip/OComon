using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OceanChip.Common.Scheduling
{
    public class LimitedConcurrencyLevelTaskScheduler : TaskScheduler
    {
        [ThreadStatic]
        private static bool _currentThreadIsProcessingItems;
        private readonly LinkedList<Task> _tasks = new LinkedList<Task>();
        private readonly int _maxDegreeOfParallelism;
        private int _delegatesQueuedOrRuning = 0;

        public LimitedConcurrencyLevelTaskScheduler(int maxDegreeOfParallelism)
        {
            if (maxDegreeOfParallelism < 1) throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));
            this._maxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(_tasks, ref lockTaken);
                if (lockTaken) return _tasks.ToArray();
                else
                    throw new NotSupportedException();
            }
            finally
            {
                if (lockTaken) Monitor.Exit(_tasks);
            }
        }

        protected override void QueueTask(Task task)
        {
            lock (_tasks)
            {
                _tasks.AddLast(task);
                if (_delegatesQueuedOrRuning < _maxDegreeOfParallelism)
                {
                    ++_delegatesQueuedOrRuning;
                    NotifyThreadPoolOfPendingWork();
                }
            }
        }

        private void NotifyThreadPoolOfPendingWork()
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                _currentThreadIsProcessingItems = true;
                try
                {
                    while (true)
                    {
                        Task item;
                        lock (_tasks)
                        {
                            if (_tasks.Count == 0)
                            {
                                --_delegatesQueuedOrRuning;
                                break;
                            }
                            item = _tasks.First.Value;
                            _tasks.RemoveFirst();
                        }
                        base.TryExecuteTask(item);
                    }
                }
                finally
                {
                    _currentThreadIsProcessingItems = false;
                }
            }, null);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (!_currentThreadIsProcessingItems) return false;

            if (taskWasPreviouslyQueued) TryDequeue(task);

            return base.TryExecuteTask(task);
        }
        protected override bool TryDequeue(Task task)
        {
            lock (_tasks) return _tasks.Remove(task);
        }
        public sealed override int MaximumConcurrencyLevel => _maxDegreeOfParallelism;
    }
}
