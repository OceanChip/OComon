using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Remoting
{
    public interface IRequestHandler
    {
        RemotingResponse HandleRequest(IRequestHandlerContext context, RemotingRequest remotingRequest);
    }
}
