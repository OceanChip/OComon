using OceanChip.Common.Socketing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Remoting
{
    public interface IRequestHandlerContext
    {
        ITcpConnection Connection { get; }
        Action<RemotingResponse> SendRemotingResponse { get; }
    }
}
