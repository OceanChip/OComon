using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Remoting
{
    public class ResponseFuture
    {
        private TaskCompletionSource<RemotingResponse> _taskSource;

        public DateTime BeginTime { get; private set; }
        public long TimeoutMillis { get; private set; }
        public RemotingRequest Request { get; private set; }

        public ResponseFuture(RemotingRequest request,long timeoutMillis,TaskCompletionSource<RemotingResponse> taskSource)
        {
            this.Request = request;
            this.TimeoutMillis = timeoutMillis;
            this._taskSource = taskSource;
            BeginTime = DateTime.Now;
        }
        public bool IsTimeout()
        {
            return (DateTime.Now - BeginTime).TotalMilliseconds > TimeoutMillis;
        }
        public bool SetResponse(RemotingResponse response)
        {
            return _taskSource.TrySetResult(response);
        }
    }
}
