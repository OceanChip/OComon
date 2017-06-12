using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Storage.Exceptions
{
    public class ChunkFileNotExistException:Exception
    {
        public ChunkFileNotExistException(string fileName) : base($"块文件{fileName}不存在")
        {

        }
    }
}
