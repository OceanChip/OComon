using OceanChip.Common.Components;
using OceanChip.Common.Logging;
using OceanChip.Common.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Socketing.Framing
{
    public class LengthPrefixMessageFramer : IMessageFramer
    {
        private static readonly ILogger _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(typeof(LengthPrefixMessageFramer));
        public const int HeaderLength = sizeof(Int32);
        private Action<ArraySegment<byte>> _receiveHandler;

        private byte[] _messageBuffer;
        private int _bufferIndex = 0;
        private int _headerBytes=0;
        private int _packageLength = 0;

        public IEnumerable<ArraySegment<byte>> FrameData(ArraySegment<byte> data)
        {
            var length = data.Count;
            yield return new ArraySegment<byte>(new[] { (byte)length, (byte)(length >> 8), (byte)(length >> 16), (byte)(length >> 24) });
            yield return data;
        }

        public void RegisterMessageArrivalCallback(Action<ArraySegment<byte>> handler)
        {
            Ensure.NotNull(handler, nameof(handler));

            _receiveHandler = handler;
        }

        public void UnFrameData(IEnumerable<ArraySegment<byte>> data)
        {
            Ensure.NotNull(data, nameof(data));

            foreach(ArraySegment<byte> item in data)
            {
                Parse(item);
            }
        }

        public void UnFrameData(ArraySegment<byte> data)
        {
            Parse(data);
        }
        private void Parse(ArraySegment<byte> bytes)
        {
            byte[] data = bytes.Array;
            for(int i = bytes.Offset, n = bytes.Offset + bytes.Count; i < n; i++)
            {
                if (_headerBytes < HeaderLength)
                {
                    _packageLength |= (data[i] << (_headerBytes * 8));
                    ++_headerBytes;
                    if (_headerBytes == HeaderLength)
                    {
                        if (_packageLength <= 0)
                            throw new Exception($"包长度{_packageLength}超界");
                        _messageBuffer = new byte[_packageLength];
                    }
                }
                else
                {
                    int copyCnt = Math.Min(bytes.Count + bytes.Offset - i, _packageLength - _bufferIndex);
                    try
                    {
                        Buffer.BlockCopy(bytes.Array, i, _messageBuffer, _bufferIndex, copyCnt);
                    }catch(Exception ex)
                    {
                        _logger.Error($"解析消息出错,_headerLength:{_headerBytes},_packageLength:{_packageLength},_bufferIndex:{_bufferIndex},Count:{copyCnt},_messageBuffer is Null:{_messageBuffer==null}",ex);
                        throw;
                    }
                    _bufferIndex += copyCnt;
                    i += copyCnt - 1;

                    if (_bufferIndex == _packageLength)
                    {
                        if (_receiveHandler != null)
                        {
                            try
                            {
                                _receiveHandler(new ArraySegment<byte>(_messageBuffer, 0, _bufferIndex));
                            }catch(Exception ex)
                            {
                                _logger.Error("处理接收到的消息错误.", ex);
                            }
                        }
                        _messageBuffer = null;
                        _headerBytes = 0;
                        _packageLength = 0;
                        _bufferIndex = 0;
                    }

                }
            }
        }
    }
}
