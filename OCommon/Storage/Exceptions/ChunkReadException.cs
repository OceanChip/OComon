using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Storage.Exceptions
{
    public class ChunkReadException:Exception
    {
        public ChunkReadException(string message) : base(message) { }
    }
}
