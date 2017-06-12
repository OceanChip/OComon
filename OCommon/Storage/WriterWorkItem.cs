using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Storage
{
    internal class WriterWorkItem
    {
        public readonly MemoryStream BufferStream;
        public readonly BinaryWriter BufferWriter;
        public readonly IStream WorkingStream;
        public long LastFlushPosition;

        public WriterWorkItem(IStream stream)
        {
            WorkingStream = stream;
            BufferStream = new MemoryStream(8192);//8K
            BufferWriter = new BinaryWriter(BufferStream);
        }
        public void AppendData(byte[] buff,int offset,int len)
        {
            WorkingStream.Write(buff, offset, len);
        }
        public void FlushToDisk()
        {
            WorkingStream.Flush();
            LastFlushPosition = WorkingStream.Position;
        }
        public void ResizeStream(long length)
        {
            WorkingStream.SetLength(length);
        }
        public void Dispose()
        {
            WorkingStream.Dispose();
        }
    }
}
