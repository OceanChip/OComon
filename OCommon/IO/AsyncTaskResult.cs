using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.IO
{
    public class AsyncTaskResult
    {
        public readonly static AsyncTaskResult Success = new AsyncTaskResult(AsyncTaskStatus.Success, null);
      
        public string ErrorMessage { get; private set; }

        public AsyncTaskResult(AsyncTaskStatus status, string errorMessage)
        {
            this.Status = status;
            this.ErrorMessage = errorMessage;
        }

        public AsyncTaskStatus Status { get; private set; }
    }
    public class AsyncTaskResult<T> : AsyncTaskResult
    {
        public T Data { get; private set; }
        public AsyncTaskResult(AsyncTaskStatus status) : this(status, null, default(T)) { }
        public AsyncTaskResult(AsyncTaskStatus status,T data) : this(status, null, data) { }
        public AsyncTaskResult(AsyncTaskStatus status,string errorMessage,T data):base(status,errorMessage)
        {
            this.Data = data;
        }
    }
    public enum AsyncTaskStatus
    {
        Success,
        IOException,
        Failed
    }
}
