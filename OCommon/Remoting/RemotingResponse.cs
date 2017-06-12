using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Remoting
{
    public class RemotingResponse
    {
        public short RequestType { get; set; }
        public short RequestCode { get; set; }
        public long RequestSequence { get; set; }
        public DateTime RequestTime { get; set; }
        public IDictionary<string, string> RequestHeader { get; set; }
        public short ResponseCode { get; set; }
        public byte[] ResponseBody { get; set; }
        public DateTime ResponseTime { get; set; }
        public IDictionary<string, string> ResponseHeader { get; set; }

        public RemotingResponse() { }
        public RemotingResponse(short requestType,short requestCode,long requestSequence,DateTime requestTime,
            short responseCode,byte[] responseBody,DateTime responseTime,IDictionary<string,string> 
            requestHeader,IDictionary<string,string> responseHeader)
        {
            this.RequestType = requestType;
            this.RequestCode = requestCode;
            this.RequestSequence = requestSequence;
            this.RequestTime = requestTime;
            this.ResponseCode = responseCode;
            this.ResponseBody = responseBody;
            this.ResponseTime = responseTime;
            this.RequestHeader = requestHeader;
            this.ResponseHeader = responseHeader;
        }
        public override string ToString()
        {
            var responseBodyLength = 0;
            if (ResponseBody != null)
                responseBodyLength = ResponseBody.Length;

            var requestTime = RequestTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var responseTime = ResponseTime.ToString("yyyy-MM-dd HH:mm:ss.fff");

            var requestHeader = string.Empty;
            if (RequestHeader != null && RequestHeader.Count > 0)
                requestHeader = string.Join(",", RequestHeader.Select(p => $"{p.Key}:{p.Value}"));
            var responseHeader = string.Empty;
            if (ResponseHeader != null && ResponseHeader.Count > 0)
                responseHeader = string.Join(",", ResponseHeader.Select(p => $"{p.Key}:{p.Value}"));
            return $"ReuqestType:{RequestType},RequestCode:{RequestCode},RequestSequence:{RequestSequence},RequestTime:{requestTime},RequestHeader:[{requestHeader}],ResponseCode:{ResponseCode},ResponseTime:{responseTime},ResponseBodyLength:{responseBodyLength},ResponseHeader:[{responseHeader}]";
        }
    }
}
