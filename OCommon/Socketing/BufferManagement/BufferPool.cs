using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Socketing.BufferManagement
{
    class BufferPool : IntelliPool<byte[]>, IBufferPool
    {
        public int BufferSize { get; private set; }
        public BufferPool(int bufferSize,int initialCount):base(initialCount,new BufferItemCreator(bufferSize))
        {
            this.BufferSize = bufferSize;
        }
    }
}
