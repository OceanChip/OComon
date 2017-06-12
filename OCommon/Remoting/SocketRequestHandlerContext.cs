using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OceanChip.Common.Socketing;

namespace OceanChip.Common.Remoting
{
    public class SocketRequestHandlerContext : IRequestHandlerContext
    {
        public ITcpConnection Connection { get; private set; }
        

        public Action<RemotingResponse> SendRemotingResponse { get; private set; }

        public SocketRequestHandlerContext(ITcpConnection connection,Action<byte[]> sendReplayAction)
        {
            this.Connection = connection;
            SendRemotingResponse = remotingResponse =>
            {
                sendReplayAction(RemotingUtil.BuildResponseMessage(remotingResponse));
            };
        }
    }
}
