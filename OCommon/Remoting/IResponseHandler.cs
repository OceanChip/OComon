using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Remoting
{
    public interface IResponseHandler
    {
        void HandleResponse(RemotingResponse remotingResponse);
    }
}
