using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Storage.Exceptions
{
    public class ChunkBadDataException:Exception
    {
        public ChunkBadDataException(string message) : base(message) { }
    }
}
