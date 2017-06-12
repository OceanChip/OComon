using OceanChip.Common.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Socketing
{
    public class SocketUtils
    {
        public static IPAddress GetLocalIPV4()
        {
            return Dns.GetHostEntry(Dns.GetHostName()).AddressList.First(p => p.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
        }
        public static Socket CreateSocket(int sendBufferSize,int receiveBufferSize)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.NoDelay = true;
            socket.Blocking = false;
            socket.SendBufferSize = sendBufferSize;
            socket.ReceiveBufferSize = receiveBufferSize;
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
            return socket;
        }
        public static void ShutdownSocket(Socket socket)
        {
            if (socket == null) return;

            Helper.ExecuteActionWithoutException(() => socket.Shutdown(SocketShutdown.Both));
            Helper.ExecuteActionWithoutException(() => socket.Close(10000));
        }
        public static void CloseSocket(Socket socket)
        {
            if (socket == null) return;
            Helper.ExecuteActionWithoutException(() => socket.Close(10000));
        }
    }
}
