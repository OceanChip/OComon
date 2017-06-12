using OceanChip.Common.Components;
using OceanChip.Common.Logging;
using OceanChip.Common.Socketing.BufferManagement;
using OceanChip.Common.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OceanChip.Common.Socketing
{
    public class ClientSocket
    {
        private EndPoint _serverEndPoint;
        private EndPoint _localEndPoint;
        private Socket _socket;
        private TcpConnection _connection;
        private readonly SocketSetting _setting;
        private readonly IList<IConnectionEventListener> _connectionEventListeners;
        private readonly Action<ITcpConnection, byte[]> _messageArrivedHandler;
        private readonly IBufferPool _receiveDataBufferPool;
        private readonly ILogger _logger;
        private readonly ManualResetEvent _waitConnectHandler;
        private readonly int _flowControlThreshold;

        public bool IsConnected => _connection != null && _connection.IsConnected;
        public TcpConnection Connection => _connection;
        public ClientSocket(EndPoint serverEndPoint,EndPoint localEndPoint,SocketSetting setting,IBufferPool receiveDataBufferPool,Action<ITcpConnection,byte[]> messageArrivedHandler)
        {
            Ensure.NotNull(serverEndPoint, nameof(serverEndPoint));
            Ensure.NotNull(setting, nameof(setting));
            Ensure.NotNull(receiveDataBufferPool, nameof(receiveDataBufferPool));
            Ensure.NotNull(messageArrivedHandler, nameof(messageArrivedHandler));

            _connectionEventListeners = new List<IConnectionEventListener>();

            _serverEndPoint = serverEndPoint;
            _setting = setting;
            _localEndPoint = localEndPoint;
            _receiveDataBufferPool = receiveDataBufferPool;
            _messageArrivedHandler = messageArrivedHandler;
            _waitConnectHandler = new ManualResetEvent(false);
            _socket = SocketUtils.CreateSocket(setting.SendBufferSize, setting.ReceiveBufferSize);
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
            _flowControlThreshold = _setting.SendMessageFlowControlThreshold;
        }
        public ClientSocket RegisterConnectionEventListener(IConnectionEventListener listener)
        {
            _connectionEventListeners.Add(listener);
            return this;
        }
        public ClientSocket Start(int waitMilliseconds = 5000)
        {
            var socketArgs = new SocketAsyncEventArgs();
            socketArgs.AcceptSocket = _socket;
            socketArgs.RemoteEndPoint = _serverEndPoint;
            socketArgs.Completed += OnConnectAsyncCompleted;
            if(_localEndPoint != null)
            {
                _socket.Bind(_localEndPoint);
            }
            var fireAsync = _socket.ConnectAsync(socketArgs);
            if (!fireAsync)
            {
                ProcessConnect(socketArgs);
            }

            _waitConnectHandler.WaitOne(waitMilliseconds);
            return this;
        }
        public ClientSocket QueueMessage(byte[] message)
        {
            _connection.QueueMessage(message);
            FlowControlIfNecessary();
            return this;
        }
        public ClientSocket Shutdown()
        {
            if (_connection != null)
            {
                _connection.Close();
                _connection = null;
            }
            else
            {
                SocketUtils.ShutdownSocket(_socket);
                _socket = null;
            }
            return this;
        }
        private void FlowControlIfNecessary()
        {
            if(_flowControlThreshold>0 && _connection.PendingMessageCount >= _flowControlThreshold)
            {
                var milliseonds = FlowControlUtil.CalculateFlowControlTimeMilliseconds(
                    (int)_connection.PendingMessageCount,
                    _flowControlThreshold,
                    _setting.SendMessageFlowControlStepPercent,
                    _setting.SendMesssageFlowControlWaitMilliseconds
                    );
                Thread.Sleep(milliseonds);
            }
        }

        private void ProcessConnect(SocketAsyncEventArgs e)
        {
            e.Completed -= OnConnectAsyncCompleted;
            e.AcceptSocket = null;
            e.RemoteEndPoint = null;
            e.Dispose();

            if(e.SocketError != SocketError.Success)
            {
                SocketUtils.ShutdownSocket(_socket);
                _logger.Info($"关闭网络链接，SocketError：{e.SocketError}");
                OnConnectionFailed(e.SocketError);
                _waitConnectHandler.Set();
                return;
            }
            _connection = new TcpConnection(_socket, _setting, _receiveDataBufferPool, OnMessageArrived, OnConnectionClosed);
            _logger.Info($"连接成功,远程地址：{_connection.RemoteEndPoint}，本地地址：{_connection.LocalEndPoint}");

            OnConnectionEstablished(_connection);

            _waitConnectHandler.Set();
        }

        private void OnConnectionFailed(SocketError socketError)
        {
            foreach (var listener in _connectionEventListeners)
            {
                try
                {
                    listener.OnConnectionFailed(socketError);
                }
                catch (Exception ex)
                {
                    _logger.Error($"通知失败,类型：{listener.GetType().Name}", ex);
                }
            }
        }

        private void OnConnectionClosed(ITcpConnection connection, SocketError socketError)
        {
            foreach (var listener in _connectionEventListeners)
            {
                try
                {
                    listener.OnConnectionClosed(connection,socketError);
                }
                catch (Exception ex)
                {
                    _logger.Error($"通知失败,类型：{listener.GetType().Name}", ex);
                }
            }
        }

        private void OnConnectionEstablished(TcpConnection connection)
        {
            foreach(var listener in _connectionEventListeners)
            {
                try
                {
                    listener.OnConnectionEstableished(connection);
                }catch(Exception ex)
                {
                    _logger.Error($"通知失败,类型：{listener.GetType().Name}", ex);
                }
            }
        }

        private void OnConnectAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            ProcessConnect(e);
        }
        private void OnMessageArrived(ITcpConnection connection,byte[] message)
        {
            try
            {
                _messageArrivedHandler(connection, message);
            }
            catch (Exception ex)
            {
                _logger.Error("消息处理异常", ex);
            }
        }
    }
}
