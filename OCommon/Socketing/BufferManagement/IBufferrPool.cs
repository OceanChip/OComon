using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Socketing.BufferManagement
{
    public interface IBufferPool:IPool<Byte[]>
    {
        int BufferSize { get; }
    }
}
