using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Storage
{
    public interface IStream
    {
        long Length { get; }
        long Position { get; set; }
        void Write(byte[] buffer, int offset, int count);
        void Flush();
        void SetLength(long value);
        void Dispose();
    }
}
