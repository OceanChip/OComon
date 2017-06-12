using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Remoting.Exceptions
{
    public class ResponseFutureAddFailedException:Exception
    {
        public ResponseFutureAddFailedException(long requestSequence):
             base(string.Format("添加远程功能失败， 请求序列:{0}", requestSequence))
        {
        }
    }
}
