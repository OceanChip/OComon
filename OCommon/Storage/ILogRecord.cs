using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Storage
{
    public interface ILogRecord
    {
        void WriteTo(long logPosition, BinaryWriter writer);
        void ReadFrom(byte[] recordBuffer);
    }
}
