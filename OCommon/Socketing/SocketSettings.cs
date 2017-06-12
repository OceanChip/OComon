using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Socketing
{
    public class SocketSetting
    {
        public int SendBufferSize = 1024 * 16;
        public int ReceiveBufferSize = 1024*16;

        public int MaxSendPacketSize = 1024 * 64;
        public int SendMessageFlowControlThreshold = 1000;
        public int SendMessageFlowControlStepPercent = 1;
        public int SendMesssageFlowControlWaitMilliseconds = 1;

        public int ReconnectedToServerInternal = 1000;
        public int ScanTimeoutRequestInterval = 1000;

        public int ReceiveDataBufferSize = 1024 * 16;
        public int ReceiveDataBufferPoolSize = 50;
    }
}
