using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Storage
{
    internal class ReaderWorkItem
    {
        public readonly Stream Stream;
        public readonly BinaryReader Reader;

        public ReaderWorkItem(Stream stream,BinaryReader reader)
        {
            this.Stream = stream;
            this.Reader = reader;
        }
    }
}
