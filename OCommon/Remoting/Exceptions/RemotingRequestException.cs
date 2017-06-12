using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Remoting.Exceptions
{
    public class RemotingRequestException:Exception
    {
        public RemotingRequestException(EndPoint serverEndPoint,RemotingRequest request,string errorMessage):
            base($"发送请求{request}到服务器{serverEndPoint}失败,失败原因:"+errorMessage)
        {

        }
        public RemotingRequestException(EndPoint serverEndPoint, RemotingRequest request,Exception ex):
            base($"发送请求{request}到服务器{serverEndPoint}失败",ex)
        {

        }

    }
}
