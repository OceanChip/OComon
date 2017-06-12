using OceanChip.Common.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OceanChip.Common.Remoting
{
    public class RemotingRequest
    {
        private static long _sequence;
        public string Id { get; set; }
        public short Type { get; set; }
        public short Code { get; set; }
        public long Sequence { get; set; }
        public byte[] Body { get; set; }
        public DateTime CreatedTime { get; set; }
        public IDictionary<string, string> Header { get; set; }
        public RemotingRequest() { }
        public RemotingRequest(short code,byte[] body,IDictionary<string,string> header = null):
            this(ObjectId.GenerateNewStringId(),code,Interlocked.Increment(ref _sequence),body,DateTime.Now,header)
        {
            
        }
        public RemotingRequest(string id,short code,long sequence,byte[] body,DateTime createTime,IDictionary<string,string> header)
        {
            this.Id = id;
            this.Code = code;
            this.Sequence = sequence;
            this.Body = body;
            this.CreatedTime = createTime;
            this.Header = header;
        }
        public override string ToString()
        {
            var createTime = this.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var bodyLength = 0;
            if (Body != null)
                bodyLength = Body.Length;

            var header = string.Empty;
            if (Header != null && Header.Count > 0)
                header = string.Join(",", Header.Select(p => $"{p.Key}:{p.Value}"));
            return $"Id:{Id},Type:{Type},Code:{Code},Sequence:{Sequence},CreatedTime{createTime},BodyLength:{bodyLength},Header:[{header}]";
        }
    }
    public class RemotingRequestType
    {
        public const short Async        = 1;
        public const short OneWay       = 2;
        public const short Callback     = 3;
    }
}
