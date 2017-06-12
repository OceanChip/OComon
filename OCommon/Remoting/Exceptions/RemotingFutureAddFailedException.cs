using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Remoting.Exceptions
{
    public class RemotingFutureAddFailedException:Exception
    {
        public RemotingFutureAddFailedException(long requestSequence):
            base($"添加远程请求响应功能失败，请求序列号：{requestSequence}")
        {

        }
    }
}
