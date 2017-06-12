using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Storage.Exceptions
{
    public class ChunkWriteException:Exception
    {
        public ChunkWriteException(string chunkName,string message) : base($"{chunkName}写错误，错误信息：{message}") { }
    }
}
