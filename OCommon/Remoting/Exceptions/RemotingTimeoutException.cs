using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Remoting.Exceptions
{
    public class RemotingTimeoutException:Exception
    {
        public RemotingTimeoutException(EndPoint serverEndPoint,RemotingRequest request,long timeoutMillis):
            base($"等待服务器({serverEndPoint})返回超时，请求：{request},超时时间：{timeoutMillis}ms")
        {

        }
    }
}
