using OceanChip.Common.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Storage
{
    public  class ChunkFooter
    {
        public const int Size = 128;
        public readonly int ChunkDataTotalSize;

        public ChunkFooter(int chunkDataTotalSize)
        {
            Ensure.Nonnegative(chunkDataTotalSize, nameof(chunkDataTotalSize));

            this.ChunkDataTotalSize = chunkDataTotalSize;
        }
        public byte[] AsByteArray()
        {
            var array = new byte[Size];
            using (var stream = new MemoryStream(array))
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(ChunkDataTotalSize);
                }
            }
            return array;
        }
        public static ChunkFooter FromStream(BinaryReader reader,Stream stream)
        {
            var chunkdatasize = reader.ReadInt32();
            return new ChunkFooter(chunkdatasize);
        }
        public override string ToString()
        {
            return $"[ChunkDataTotalSize:{ChunkDataTotalSize}]";
        }
    }
}
