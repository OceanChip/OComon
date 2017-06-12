using OceanChip.Common.Components;
using OceanChip.Common.Logging;
using OceanChip.Common.Socketing;
using OceanChip.Common.Socketing.BufferManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Remoting
{
    public class SocketRemotingServer
    {
        private readonly ServerSocket _serverSocket;
        private readonly Dictionary<int, IRequestHandler> _requestHandlerDict;
        private readonly IBufferPool _receiveDataBufferPool;
        private readonly ILogger _logger;
        private readonly SocketSetting _setting;
        private bool _isShuttingdown = false;

        public IBufferPool BufferPool => _receiveDataBufferPool;
        

        public SocketRemotingServer():this("Server",new IPEndPoint(SocketUtils.GetLocalIPV4(), 5000)) { }
        public SocketRemotingServer(string name,IPEndPoint listeningEndPoint,SocketSetting setting = null)
        {
            _setting = setting ?? new SocketSetting();
            _receiveDataBufferPool = new BufferPool(_setting.ReceiveDataBufferSize, _setting.ReceiveDataBufferPoolSize);
            _serverSocket = new ServerSocket(listeningEndPoint, _setting, _receiveDataBufferPool, HandleRemotingRequest);
            _requestHandlerDict = new Dictionary<int, IRequestHandler>();
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
        }
        public SocketRemotingServer RegisterConnectionEventListener(IConnectionEventListener listener)
        {
            _serverSocket.RegisterConnectionEventListener(listener);
            return this;
        }
        public SocketRemotingServer Start()
        {
            _isShuttingdown = false;
            _serverSocket.Start();
            return this;
        }
        public SocketRemotingServer Shutdown()
        {
            _isShuttingdown = true;
            _serverSocket.Shutdown();
            return this;
        }
        public SocketRemotingServer RegisterRequestHandler(int requestCode,IRequestHandler handler)
        {
            _requestHandlerDict[requestCode] = handler;
            return this;
        }
        private void HandleRemotingRequest(ITcpConnection connection, byte[] message, Action<byte[]> sendReplyAction)
        {
            if (_isShuttingdown) return;

            var remotingRequest = RemotingUtil.ParseRequest(message);
            var requestHandlerContext = new SocketRequestHandlerContext(connection, sendReplyAction);

            IRequestHandler handler;
            if(!_requestHandlerDict.TryGetValue(remotingRequest.Code,out handler))
            {
                var errorMsg = $"远程未发现请求:{remotingRequest}";
                _logger.Error(errorMsg);
                if(remotingRequest.Type != RemotingRequestType.OneWay)
                {
                    requestHandlerContext.SendRemotingResponse(new RemotingResponse(
                        remotingRequest.Type,
                        remotingRequest.Code,
                        remotingRequest.Sequence,
                        remotingRequest.CreatedTime,
                        -1,
                        Encoding.UTF8.GetBytes(errorMsg),
                        DateTime.Now,
                        remotingRequest.Header,
                        null));
                }
                return;
            }
            try
            {
                var remotingResponse = handler.HandleRequest(requestHandlerContext, remotingRequest);
                if(remotingRequest.Type !=RemotingRequestType.OneWay&& remotingResponse != null)
                {
                    requestHandlerContext.SendRemotingResponse(remotingResponse);
                }
            }catch(Exception ex)
            {
                var errorMsg = $"远程请求句柄引起未知异常：{remotingRequest}";
                _logger.Error(errorMsg, ex);
                if(remotingRequest.Type != RemotingRequestType.OneWay)
                {
                    requestHandlerContext.SendRemotingResponse(new RemotingResponse(
                        remotingRequest.Type,
                        remotingRequest.Code,
                        remotingRequest.Sequence,
                        remotingRequest.CreatedTime,
                        -1,
                        Encoding.UTF8.GetBytes(errorMsg),
                        DateTime.Now,
                        remotingRequest.Header,
                        null));
                }
            }
        }
    }
}
