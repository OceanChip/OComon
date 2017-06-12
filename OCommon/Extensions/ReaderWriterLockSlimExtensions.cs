using OceanChip.Common.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OceanChip.Common.Extensions
{
    public static class ReaderWriterLockSlimExtensions
    {
        public static void AtomRead(this ReaderWriterLockSlim readerwriterSlim, Action action)
        {
            Ensure.NotNull(readerwriterSlim, nameof(readerwriterSlim));
            Ensure.NotNull(action, nameof(action));

            readerwriterSlim.EnterReadLock();
            try
            {
                action();
            }
            finally
            {
                readerwriterSlim.ExitReadLock();
            }
        }
        public static T AtomRead<T>(this ReaderWriterLockSlim readerwriterSlim, Func<T> function)
        {
            Ensure.NotNull(readerwriterSlim, nameof(readerwriterSlim));
            Ensure.NotNull(function, nameof(function));

            readerwriterSlim.EnterReadLock();
            try
            {
                return function();
            }
            finally
            {
                readerwriterSlim.ExitReadLock();
            }
        }
        public static void AtomWriter(this ReaderWriterLockSlim readerwriterSlim, Action action)
        {
            Ensure.NotNull(readerwriterSlim, nameof(readerwriterSlim));
            Ensure.NotNull(action, nameof(action));

            readerwriterSlim.EnterWriteLock();
            try
            {
                action();
            }
            finally
            {
                readerwriterSlim.ExitWriteLock();
            }
        }
        public static T AtomWriter<T>(this ReaderWriterLockSlim readerwriterSlim, Func<T> function)
        {
            Ensure.NotNull(readerwriterSlim, nameof(readerwriterSlim));
            Ensure.NotNull(function, nameof(function));

            readerwriterSlim.EnterWriteLock();
            try
            {
                return function();
            }
            finally
            {
                readerwriterSlim.ExitWriteLock();
            }
        }
    }
}
