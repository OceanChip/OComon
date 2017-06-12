using OceanChip.Common.Components;
using OceanChip.Common.Logging;
using OceanChip.Common.Scheduling;
using OceanChip.Common.Socketing;
using OceanChip.Common.Socketing.BufferManagement;
using OceanChip.Common.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Threading;
using OceanChip.Common.Remoting.Exceptions;
using OceanChip.Common.Extensions;

namespace OceanChip.Common.Remoting
{
    public class SocketRemotingClient
    {
        private readonly byte[] TimeoutMessage = Encoding.UTF8.GetBytes("Remoting request timeout.");
        private readonly Dictionary<int, IResponseHandler> _responseHandlerDict;
        private readonly IList<IConnectionEventListener> _connectionEventListeners;
        private readonly ConcurrentDictionary<long, ResponseFuture> _responseFutureDict;
        private readonly BlockingCollection<byte[]> _replyMessageQueue;
        private readonly IScheduleService _scheduleService;
        private readonly IBufferPool _receiveDataBufferPool;
        private readonly ILogger _logger;
        private readonly SocketSetting _setting;

        private EndPoint _serverEndPoint;
        private EndPoint _localEndPoint;
        private ClientSocket _clientSocket;
        private int _reconnecting = 0;
        private bool _shutteddown = false;
        private bool _started = false;

        public bool IsConnected => _clientSocket != null && _clientSocket.IsConnected;

        public EndPoint LocalEndPoint => _localEndPoint;
        public EndPoint ServerEndPoint => _serverEndPoint;
        public ClientSocket ClientSocket => _clientSocket;
        public IBufferPool BufferPool => _receiveDataBufferPool;

        public SocketRemotingClient():this(new IPEndPoint(SocketUtils.GetLocalIPV4(), 5000))
        {

        }
        public SocketRemotingClient(EndPoint serverEndPoint,SocketSetting setting=null,EndPoint locklEndPoint = null)
        {
            Ensure.NotNull(serverEndPoint, nameof(serverEndPoint));

            this._serverEndPoint = serverEndPoint;
            this._localEndPoint = LocalEndPoint;
            this._setting = setting??new SocketSetting();
            this._receiveDataBufferPool = new BufferPool(_setting.ReceiveBufferSize, _setting.ReceiveDataBufferPoolSize);
            this._clientSocket = new ClientSocket(_serverEndPoint, _localEndPoint, _setting, _receiveDataBufferPool, HandlerReplyMessage);
            this._responseFutureDict = new ConcurrentDictionary<long, ResponseFuture>();
            this._replyMessageQueue = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>());
            this._responseHandlerDict = new Dictionary<int, IResponseHandler>();
            this._connectionEventListeners = new List<IConnectionEventListener>();
            this._scheduleService = ObjectContainer.Resolve<IScheduleService>();
            this._logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);

            RegisterConnectionEventLister(new ConnectionEventListener(this));
        }
        public  SocketRemotingClient RegisterResponseHandler(int requestCode,IResponseHandler responseHander)
        {
            _responseHandlerDict[requestCode] = responseHander;
            return this;
        }
        public SocketRemotingClient RegisterConnectionEventLister(IConnectionEventListener listener)
        {
            _connectionEventListeners.Add(listener);
            _clientSocket.RegisterConnectionEventListener(listener);
            return this;
        }
        public SocketRemotingClient Start()
        {
            if (_started) return this;

            StartClientSocket();
            StartScanTimeoutRequestTask();
            _shutteddown = false;
            _started = true;
            return this;
        }
        public void Shutdown()
        {
            _shutteddown = true;
            StopReconnectServerTask();
            StopScanTimeoutRequestTask();
            ShutdownClientSocket();
        }
        public RemotingResponse InvokeSync(RemotingRequest request,int timeoutMillis)
        {
            var task = InvokeAsync(request, timeoutMillis);
            var response = task.WaitResult<RemotingResponse>(timeoutMillis + 1000);

            if(response == null)
            {
                if (!task.IsCompleted)
                {
                    throw new RemotingTimeoutException(_serverEndPoint, request, timeoutMillis);
                }else if (task.IsFaulted)
                {
                    throw new RemotingRequestException(_serverEndPoint, request, task.Exception);
                }
                else
                {
                    throw new RemotingRequestException(_serverEndPoint, request, "未知错误");
                }
            }
            return response;
        }
        public Task<RemotingResponse> InvokeAsync(RemotingRequest request,int timeoutMillis)
        {
            EnsureClientStatus();

            request.Type = RemotingRequestType.Async;
            var taskCompletionSource = new TaskCompletionSource<RemotingResponse>();
            var responseFuture = new ResponseFuture(request, timeoutMillis, taskCompletionSource);

            if (!_responseFutureDict.TryAdd(request.Sequence, responseFuture))
            {
                throw new ResponseFutureAddFailedException(request.Sequence);
            }

            _clientSocket.QueueMessage(RemotingUtil.BuildRequestMessage(request));
            return taskCompletionSource.Task;
        }
        public void InvokeWithCallback(RemotingRequest request)
        {
            SetRequestMessage(request, RemotingRequestType.Callback);
        }
        public void InvokeOnway(RemotingRequest request)
        {
            SetRequestMessage(request, RemotingRequestType.OneWay);
        }
        private void SetRequestMessage(RemotingRequest request, short type)
        {
            EnsureClientStatus();

            request.Type = type;
            _clientSocket.QueueMessage(RemotingUtil.BuildRequestMessage(request));
        }
        private void HandlerReplyMessage(ITcpConnection connection, byte[] message)
        {
            if (message == null) return;

            var remotingResponse = RemotingUtil.ParseResponse(message);

            if(remotingResponse.RequestType == RemotingRequestType.Callback)
            {
                IResponseHandler responseHandler;
                if(_responseHandlerDict.TryGetValue(remotingResponse.ResponseCode,out responseHandler))
                {
                    responseHandler.HandleResponse(remotingResponse);
                }
                else
                {
                    _logger.Error($"远端未发现请求的回调");
                }
            }
            else if(remotingResponse.RequestType == RemotingRequestType.Async)
            {
                ResponseFuture responseFeture;
                if(_responseFutureDict.TryRemove(remotingResponse.RequestSequence,out responseFeture))
                {
                    if (responseFeture.SetResponse(remotingResponse))
                    {
                        if (_logger.IsDebugEnabled)
                        {
                            _logger.Debug($"远端返回，请求码:{responseFeture.Request.Code},序列：{responseFeture.Request.Sequence},时间花费：{(DateTime.Now-responseFeture.BeginTime).TotalMilliseconds}");
                        }
                    }
                    else
                    {
                        _logger.Error($"设置远端请求失败:{remotingResponse}");
                    }
                }
            }
        }
        private void StartClientSocket()
        {
            _clientSocket.Start();
        }
        private void ShutdownClientSocket()
        {
            _clientSocket.Shutdown();
        }
        class ConnectionEventListener : IConnectionEventListener
        {
            private readonly SocketRemotingClient _remotingClient;
            public ConnectionEventListener(SocketRemotingClient remotingClient)
            {
                this._remotingClient = remotingClient;
            }
            public void OnConnectionAccepted(ITcpConnection connection)
            {

            }

            public void OnConnectionClosed(ITcpConnection connection, SocketError socketError)
            {
                if (_remotingClient._shutteddown) return;

                _remotingClient.ExitReconnecting();
                _remotingClient.StartReconnectServerTask();
            }

            public void OnConnectionEstableished(ITcpConnection connection)
            {
                _remotingClient.StopReconnectServerTask();
                _remotingClient.ExitReconnecting();
                _remotingClient.SetLocalEndPoint(connection.LocalEndPoint);
            }

            public void OnConnectionFailed(SocketError socketError)
            {
                if (_remotingClient._shutteddown) return;

                _remotingClient.ExitReconnecting();
                _remotingClient.StartReconnectServerTask();
            }
        }

        private void StartScanTimeoutRequestTask()
        {
            _scheduleService.StartTask($"{GetType().Name}.ScanTimeoutRequest", ScanTimeoutRequest, 1000, _setting.ScanTimeoutRequestInterval);

        }
        private void StopScanTimeoutRequestTask()
        {
            _scheduleService.StopTask($"{GetType().Name}.ScanTimeoutRequest");
        }
        private void ScanTimeoutRequest()
        {
            var timeoutKeyList = new List<long>();
            foreach(var entry in _responseFutureDict)
            {
                if (entry.Value.IsTimeout())
                {
                    timeoutKeyList.Add(entry.Key);
                }
            }

            foreach(var key in timeoutKeyList)
            {
                ResponseFuture responseFeture;
                if(_responseFutureDict.TryRemove(key,out responseFeture))
                {
                    var request = responseFeture.Request;
                    responseFeture.SetResponse(new RemotingResponse(
                        request.Type,
                        request.Code,
                        request.Sequence,
                        request.CreatedTime,
                        0, TimeoutMessage,
                        DateTime.Now, request.Header,
                        null));
                    if (_logger.IsDebugEnabled)
                    {
                        _logger.Debug($"移除超时请求：{responseFeture.Request}");
                    }
                }
            }
        }

        private void StartReconnectServerTask()
        {
            _scheduleService.StartTask($"{GetType().Name}.ReconnectServer",ReconnectServer, 1000, _setting.ReconnectedToServerInternal);
        }

        private void StopReconnectServerTask()
        {
            _scheduleService.StopTask($"{GetType().Name}.ReconnectServer");
        }
        private void ReconnectServer()
        {
            _logger.Info($"尝试重连服务器，服务器地址：{_serverEndPoint}");

            if (_clientSocket.IsConnected) return;
            if (!EnterReconnecting()) return;

            try
            {
                _clientSocket.Shutdown();
                _clientSocket = new ClientSocket(_serverEndPoint, _localEndPoint, _setting, _receiveDataBufferPool, HandlerReplyMessage);
                foreach(var listener in _connectionEventListeners)
                {
                    _clientSocket.RegisterConnectionEventListener(listener);
                }
                _clientSocket.Start();
            }catch(Exception ex)
            {
                _logger.Error("重连服务器失败", ex);
                ExitReconnecting();
            }

        }

        private void SetLocalEndPoint(EndPoint localEndPoint)
        {
            _localEndPoint = localEndPoint;
        }
        private bool EnterReconnecting()
        {
            return Interlocked.CompareExchange(ref _reconnecting, 1, 0) == 0;
        }
        private void ExitReconnecting()
        {
            Interlocked.Exchange(ref _reconnecting, 0);
        }
        private void EnsureClientStatus()
        {
            if(_clientSocket==null || !_clientSocket.IsConnected)
            {
                throw new RemotingServerUnAvailableException(_serverEndPoint);
            }
        }

    }
}
