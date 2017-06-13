using OceanChip.Common.Components;
using OceanChip.Common.Logging;
using OceanChip.Common.Socketing.BufferManagement;
using OceanChip.Common.Socketing.Framing;
using OceanChip.Common.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OceanChip.Common.Socketing
{
    public class TcpConnection : ITcpConnection
    {
        private Socket _socket;
        private readonly SocketSetting _settings;
        private readonly EndPoint _localEndPoint;
        private readonly EndPoint _remoteEndPoint;
        private readonly SocketAsyncEventArgs _sendSocketArgs;
        private readonly SocketAsyncEventArgs _receiveSocketArgs;
        private readonly IBufferPool _receiveDataBufferPool;
        private readonly IMessageFramer _framer;
        private readonly ILogger _logger;
        private readonly ConcurrentQueue<IEnumerable<ArraySegment<byte>>> _sendingQueue = new ConcurrentQueue<IEnumerable<ArraySegment<byte>>>();
        private readonly ConcurrentQueue<ReceivedData> _receiveQueue = new ConcurrentQueue<ReceivedData>();
        private readonly MemoryStream _sendingStream = new MemoryStream();

        private Action<ITcpConnection, SocketError> _connectionClosedHandler;
        private Action<ITcpConnection, byte[]> _messageArrivedHandler;

        private int _sending;
        private int _receiving;
        private int _paring;
        private int _closing;

        private long _pendingMessageCount;

        public TcpConnection(Socket socket,SocketSetting setting,IBufferPool receiveDataBufferPool,Action<ITcpConnection,byte[]> messageArrivalHandler,Action<ITcpConnection,SocketError> connectionCloseHandler)
        {
            Check.NotNull(socket, nameof(socket));
            Check.NotNull(setting, nameof(setting));
            Check.NotNull(receiveDataBufferPool, nameof(receiveDataBufferPool));
            Check.NotNull(messageArrivalHandler, nameof(messageArrivalHandler));
            Check.NotNull(connectionCloseHandler, nameof(connectionCloseHandler));

            _socket = socket;
            this._settings = setting;
            this._receiveDataBufferPool = receiveDataBufferPool;
            this._localEndPoint = socket.LocalEndPoint;
            this._remoteEndPoint = socket.RemoteEndPoint;
            this._messageArrivedHandler = messageArrivalHandler;
            this._connectionClosedHandler = connectionCloseHandler;

            _sendSocketArgs = new SocketAsyncEventArgs();
            _sendSocketArgs.AcceptSocket = socket;
            _sendSocketArgs.Completed += OnSendAsyncCompleted;

            _receiveSocketArgs = new SocketAsyncEventArgs();
            _receiveSocketArgs.AcceptSocket = socket;
            _receiveSocketArgs.Completed += OnReceiveAsyncCompleted;

            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
            _framer = ObjectContainer.Resolve<IMessageFramer>();
            _framer.RegisterMessageArrivalCallback(OnMessageArrived);

            TryReceive();
            TrySend();
        }

        private void OnMessageArrived(ArraySegment<byte> messageSegment)
        {
            byte[] message = new byte[messageSegment.Count];
            Buffer.BlockCopy(messageSegment.Array, messageSegment.Offset, message, 0, messageSegment.Count);
            try
            {
                _messageArrivedHandler(this, message);
            }catch(Exception ex)
            {
                _logger.Error("调用消息接收事件失败", ex);
            }
        }

        private void OnReceiveAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            ProcessReceive(e);
        }

        private void OnSendAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            ProcessSend(e);
        }

        public bool IsConnected => _socket!=null && _socket.Connected;

        public EndPoint LocalEndPoint => _localEndPoint;

        public EndPoint RemoteEndPoint => _remoteEndPoint;

        public Socket Socket => _socket;
        public SocketSetting Setting => _settings;
        public long PendingMessageCount => _pendingMessageCount;

        public void Close()
        {
            CloseInternal(SocketError.Success, "Socket正常关闭", null);
        }

        public void QueueMessage(byte[] message)
        {
            if (message.Length == 0)
                return;
            var segments = _framer.FrameData(new ArraySegment<byte>(message, 0, message.Length));
            _sendingQueue.Enqueue(segments);
            Interlocked.Increment(ref _pendingMessageCount);

            TrySend();
        }
        #region 发送
        private void TrySend()
        {
            if (_closing == 1) return;
            if (!EnterSending()) return;

            _sendingStream.SetLength(0);

            IEnumerable < ArraySegment < byte >> segments;
            while(_sendingQueue.TryDequeue(out segments))
            {
                Interlocked.Decrement(ref _pendingMessageCount);
                foreach(var seg in segments)
                {
                    _sendingStream.Write(seg.Array, seg.Offset, seg.Count);
                }
                if (_sendingStream.Length >= _settings.MaxSendPacketSize)
                    break;
            }
            if (_sendingStream.Length == 0)
            {
                ExitSending();
                if (_sendingQueue.Count > 0)
                    TrySend();
                return;
            }
            try
            {
                _sendSocketArgs.SetBuffer(_sendingStream.GetBuffer(), 0, (int)_sendingStream.Length);
                var fireAsync = _sendSocketArgs.AcceptSocket.SendAsync(_sendSocketArgs);
                if (!fireAsync)
                {
                    ProcessSend(_sendSocketArgs);
                }
            }catch(Exception ex)
            {
                CloseInternal(SocketError.Shutdown, "Socket发送错误，错误信息：" + ex.Message, ex);
                ExitSending();
            }
        }

        private void ExitSending()
        {
            Interlocked.Exchange(ref _sending, 0);
        }

        private bool EnterSending()
        {
            return Interlocked.CompareExchange(ref _sending, 1, 0) == 0;
        }

        private void ProcessSend(SocketAsyncEventArgs socketArgs)
        {
            if (_closing == 1) return;
            if (socketArgs.Buffer != null)
            {
                socketArgs.SetBuffer(null, 0, 0);
            }
            ExitSending();

            if (socketArgs.SocketError == SocketError.Success)
                TrySend();
            else
            {
                CloseInternal(SocketError.Shutdown, "Socket发送错误" , null);
            }
        }
        #endregion
        #region 接收
        private void TryReceive()
        {
            if (!EntryReceiving()) return;

            var buffer = _receiveDataBufferPool.Get();
            if(buffer ==null)
            {
                CloseInternal(SocketError.Shutdown, "Socket接收分配缓存失败", null);
                ExitReceiving();
                return;
            }
            try
            {
                _receiveSocketArgs.SetBuffer(buffer, 0, buffer.Length);
                if(_receiveSocketArgs.Buffer == null)
                {
                    CloseInternal(SocketError.Shutdown, "Socket设置缓存失败", null);
                    ExitReceiving();
                    return;
                }

                bool fireAsync = _receiveSocketArgs.AcceptSocket.ReceiveAsync(_receiveSocketArgs);
                if (!fireAsync)
                {
                    ProcessReceive(_receiveSocketArgs);
                }
            }catch(Exception ex)
            {
                CloseInternal(SocketError.Shutdown, "Socket接收数据异常，异常信息：" + ex.Message, ex);
                ExitReceiving();
            }
        }
        private void ProcessReceive(SocketAsyncEventArgs socketArgs)
        {
            if(socketArgs.BytesTransferred==0 || socketArgs.SocketError != SocketError.Success)
            {
                CloseInternal(socketArgs.SocketError, socketArgs.SocketError != SocketError.Success ? "Socket接收错误" : "Socket正常关闭", null);
                return;
            }

            try
            {
                var segment = new ArraySegment<byte>(socketArgs.Buffer, socketArgs.Offset, socketArgs.Count);
                _receiveQueue.Enqueue(new ReceivedData(segment, socketArgs.BytesTransferred));
                socketArgs.SetBuffer(null, 0, 0);

                TryParsingReceivedData();
            }catch(Exception ex)
            {
                CloseInternal(SocketError.Shutdown, "解析接收数据错误", ex);
                return;
            }
            ExitReceiving();
            TryReceive();
        }
        private void TryParsingReceivedData()
        {
            if (!EntryParsing()) return;

            try
            {
                var dataList = new List<ReceivedData>(_receiveQueue.Count);
                var segmentList = new List<ArraySegment<byte>>();

                ReceivedData data;
                while(_receiveQueue.TryDequeue(out data))
                {
                    dataList.Add(data);
                    segmentList.Add(new ArraySegment<byte>(data.Buf.Array, data.Buf.Offset, data.DataLength));
                }
                _framer.UnFrameData(segmentList);

                for(int i=0,n=dataList.Count; i < n; i++)
                {
                    _receiveDataBufferPool.Return(dataList[i].Buf.Array);
                }
            }
            finally
            {
                ExitParsing();
            }
        }
        private bool EntryReceiving()
        {
            return Interlocked.CompareExchange(ref _receiving, 1, 0) == 0;
        }
        private void ExitReceiving()
        {
            Interlocked.Exchange(ref _receiving, 0);
        }
        private bool EntryParsing()
        {
            return Interlocked.CompareExchange(ref _paring, 1, 0) == 0;
        }
        private void ExitParsing()
        {
            Interlocked.Exchange(ref _paring, 0);
        }
        struct ReceivedData
        {
            public readonly ArraySegment<byte> Buf;
            public readonly int DataLength;
            public ReceivedData(ArraySegment<byte> buf, int dataLen)
            {
                this.Buf = buf;
                this.DataLength = dataLen;
            }
        }
        #endregion

        private void CloseInternal(SocketError socketError, string reason, Exception exception)
        {
            if(Interlocked.CompareExchange(ref _closing, 1, 0) == 0)
            {
                try
                {
                    if(_receiveSocketArgs.Buffer != null)
                    {
                        _receiveDataBufferPool.Return(_receiveSocketArgs.Buffer);
                    }
                }catch(Exception ex)
                {
                    _logger.Error("回收缓存失败",ex);
                }

                Helper.ExecuteActionWithoutException(() =>
                {
                    if(_sendSocketArgs != null)
                    {
                        _sendSocketArgs.Completed -= OnSendAsyncCompleted;
                        _sendSocketArgs.AcceptSocket = null;
                        _sendSocketArgs.Dispose();
                    }
                    if (_receiveSocketArgs != null)
                    {
                        _receiveSocketArgs.Completed -= OnReceiveAsyncCompleted;
                        _receiveSocketArgs.AcceptSocket = null;
                        _receiveSocketArgs.Dispose();
                    }
                });

                SocketUtils.ShutdownSocket(_socket);
                var isDisposedException = exception != null && exception is ObjectDisposedException;
                if (!isDisposedException)
                {
                    _logger.Info($"Socket以关闭，远程节点：{RemoteEndPoint},SocketError:{socketError}，原因：{reason},异常：{exception}");
                }
                _socket = null;

                if(_connectionClosedHandler != null)
                {
                    try
                    {
                        _connectionClosedHandler(this, socketError);
                    }catch(Exception ex)
                    {
                        _logger.Error("调用ConnectionCloseHandler失败", ex);
                    }
                }
            }

        }
    }
    

    public interface ITcpConnection
    {
        bool IsConnected { get; }
        EndPoint LocalEndPoint { get; }
        EndPoint RemoteEndPoint { get; }
        void QueueMessage(byte[] message);
        void Close();
    }
}
