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
    public class ServerSocket
    {
        private readonly Socket _socket;
        private readonly SocketSetting _setting;
        private readonly IPEndPoint _listeningEndPoint;
        private readonly SocketAsyncEventArgs _acceptSocketArgs;
        private readonly IList<IConnectionEventListener> _connectionEventListeners;
        private readonly Action<ITcpConnection, byte[], Action<byte[]>> _messageArrivedHandler;
        private readonly IBufferPool _receiveDataBufferPool;
        private readonly ILogger _logger;

        public ServerSocket(IPEndPoint endpoint,SocketSetting setting,IBufferPool receiveDataBufferPool, Action<ITcpConnection, byte[], Action<byte[]>> messageArrivedHandler)
        {
            Ensure.NotNull(endpoint, nameof(endpoint));
            Ensure.NotNull(setting, nameof(setting));
            Ensure.NotNull(receiveDataBufferPool, nameof(receiveDataBufferPool ));
            Ensure.NotNull(messageArrivedHandler, nameof(messageArrivedHandler));

            _listeningEndPoint = endpoint;
            _setting = setting;
            _receiveDataBufferPool = receiveDataBufferPool;
            _connectionEventListeners = new List<IConnectionEventListener>();
            _messageArrivedHandler = messageArrivedHandler;
            _socket = SocketUtils.CreateSocket(_setting.SendBufferSize, _setting.ReceiveBufferSize);
            _acceptSocketArgs = new SocketAsyncEventArgs();
            _acceptSocketArgs.Completed += AcceptCompleted;
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);

        }
        public void RegisterConnectionEventListener(IConnectionEventListener listener)
        {
            _connectionEventListeners.Add(listener);
        }
        public void Start()
        {
            _logger.Info($"Socket服务开始启动，监听地址：{_listeningEndPoint}");

            try
            {
                _socket.Bind(_listeningEndPoint);
                _socket.Listen(5000);
            }catch(Exception ex)
            {
                _logger.Error($"开启Tcp监听失败，监听地址：{_listeningEndPoint}", ex);
                SocketUtils.ShutdownSocket(_socket);
                throw;
            }
            StartAccepting();
        }
        public void Shutdown()
        {
            SocketUtils.ShutdownSocket(_socket);
            _logger.Info($"关闭网络监听,地址：{_listeningEndPoint}");
        }
        private void StartAccepting()
        {
            try
            {
                var fireAsync = _socket.AcceptAsync(_acceptSocketArgs);
                if (!fireAsync)
                {
                    ProcessAccept(_acceptSocketArgs);
                }
            }catch(Exception ex)
            {
                if(!(ex is ObjectDisposedException))
                {
                    _logger.Info("Socket接收异常，1秒后尝试重新开启",ex);
                }
                Thread.Sleep(1000);
                StartAccepting();
            }
        }

        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            try
            {
                if(e.SocketError == SocketError.Success)
                {
                    var acceptSocket = e.AcceptSocket;
                    e.AcceptSocket = null;
                    OnSocketAccepted(acceptSocket);
                }
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex){
                _logger.Error("处理网络请求异常", ex);
            }
            finally
            {
                StartAccepting();
            }
        }

        private void OnSocketAccepted(Socket socket)
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                    var connection = new TcpConnection(socket, _setting, _receiveDataBufferPool, OnMessageArrived, OnConnectionClosed);
                    _logger.Info("网络请求，远端地址：" + socket.RemoteEndPoint);
                    foreach(var listener in _connectionEventListeners)
                    {
                        try
                        {
                            listener.OnConnectionAccepted(connection);
                        }catch(Exception ex)
                        {
                            _logger.Error($"通知网络接收连接失败,类型：{listener.GetType().Name}", ex);
                        }
                    }
                }
                catch (ObjectDisposedException) { }
                catch(Exception ex)
                {
                    _logger.Error("等待客户端连接发生未知异常", ex);
                }
            });
        }
        private void OnMessageArrived(ITcpConnection connection,byte[] message)
        {
            try
            {
                _messageArrivedHandler(connection, message,reply=>connection.QueueMessage(reply));
            }catch(Exception ex)
            {
                _logger.Error("消息处理异常", ex);
            }
        }
        private void OnConnectionClosed(ITcpConnection connection,SocketError socketError)
        {
            foreach(var listener in _connectionEventListeners)
            {
                try
                {
                    listener.OnConnectionClosed(connection, socketError);
                }catch(Exception ex)
                {
                    _logger.Error($"通知网络接收连接失败,类型：{listener.GetType().Name}", ex);
                }
            }
        }
        private void AcceptCompleted(object sender, SocketAsyncEventArgs e)
        {
            ProcessAccept(e);
        }
    }
}
