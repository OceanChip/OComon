using OceanChip.Common.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OceanChip.Common.Utilities
{
    public class BufferQueue<TMessage>
    {
        //请求阀值
        public int _requestsWriteThreshold;
        //输入队列
        private ConcurrentQueue<TMessage> _inputQueue;
        //消息处理队列
        private ConcurrentQueue<TMessage> _processQueue;
        private Action<TMessage> _handleMessageAction;
        private readonly string _name;
        private readonly ILogger _logger;
        private int _isProcessingMessage=0;

        public BufferQueue(string name,int requestsWriteThreshold,Action<TMessage> handleMessageAction,ILogger logger)
        {
            this._name = name;
            this._requestsWriteThreshold = requestsWriteThreshold;
            this._handleMessageAction = handleMessageAction;
            this._inputQueue = new ConcurrentQueue<TMessage>();
            this._processQueue = new ConcurrentQueue<TMessage>();
            this._logger = logger;
        }
        public void EnqueueMessage(TMessage message)
        {
            _inputQueue.Enqueue(message);
            TryProcessMessage();

            if (_inputQueue.Count >= _requestsWriteThreshold)
            {
                Thread.Sleep(1);
            }
        }
        /// <summary>
        /// 执行消息处理
        /// </summary>
        private void TryProcessMessage()
        {
            if(Interlocked.CompareExchange(ref _isProcessingMessage, 1, 0) == 0)
            {
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        if(_processQueue.Count==0 && _inputQueue.Count > 0)
                        {
                            //交换执行队列与输入队列
                            SwapInputQueue();
                        }
                        if (_processQueue.Count > 0)
                        {
                            var count = 0;
                            TMessage message;
                            while(_processQueue.TryDequeue(out message))
                            {
                                try
                                {
                                    _handleMessageAction(message);
                                }catch(Exception ex)
                                {
                                    var errorMessage = _name + " 处理消息发生异常.";
                                    if (_logger != null)
                                        _logger.Error(errorMessage, ex);
                                }
                                finally
                                {
                                    count++;
                                }
                                if (_logger.IsDebugEnabled)
                                {
                                    _logger.Debug($"BufferQueue[name={_name}],批量执行{count}条消息。");
                                }
                            }
                        }
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _isProcessingMessage, 0);
                        if (_inputQueue.Count > 0)
                            TryProcessMessage();
                    }
                });
            }
        }
        /// <summary>
        /// 交换队列
        /// </summary>
        private void SwapInputQueue()
        {
            var tmp = _inputQueue;
            _inputQueue = _processQueue;
            _processQueue = tmp;
        }
    }
}
