using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Remoting.Exceptions
{
    public class RemotingServerUnAvailableException:Exception
    {
        public RemotingServerUnAvailableException(EndPoint serverEndPoint):
            base($"远端服务无效，服务地址：{serverEndPoint}")
        {

        }
    }
}
