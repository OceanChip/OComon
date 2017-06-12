using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Storage.Exceptions
{
    public class ChunkNotExistException:Exception
    {
        public ChunkNotExistException(long position,int chunkNum) : base($"块不存在，起始地址：{position},块数：{chunkNum}") { }
    }
}
