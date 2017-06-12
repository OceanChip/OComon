using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Socketing
{
    public interface IConnectionEventListener
    {
        void OnConnectionAccepted(ITcpConnection connection);
        void OnConnectionEstableished(ITcpConnection connection);
        void OnConnectionFailed(SocketError socketError);
        void OnConnectionClosed(ITcpConnection connection, SocketError socketError);
    }
}
