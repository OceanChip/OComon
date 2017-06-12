using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Storage.Exceptions
{
    public class ChunkCreateException:Exception
    {
        public ChunkCreateException(string message) : base(message) { }
    }
}
