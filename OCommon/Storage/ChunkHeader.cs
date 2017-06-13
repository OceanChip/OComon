using OceanChip.Common.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Storage
{
    public class ChunkHeader
    {
        public const int Size = 128;
        public readonly int ChunkNumber;
        public readonly int ChunkDataTotalSize;
        public readonly long ChunkDataStartPosition;
        public readonly long ChunkDataEndPosition;

        public ChunkHeader(int chunkNumber,int chunkDataTotalSize)
        {
            Check.Nonnegative(chunkNumber, nameof(chunkNumber));
            Check.Positive(chunkDataTotalSize, nameof(chunkDataTotalSize));

            ChunkNumber = chunkNumber;
            this.ChunkDataTotalSize = chunkDataTotalSize;

            ChunkDataStartPosition = ChunkNumber * (long)ChunkDataTotalSize;
            this.ChunkDataEndPosition = (this.ChunkNumber + 1) * (long)this.ChunkDataTotalSize;
        }
        public byte[] AsByteArray()
        {
            var array = new byte[Size];
            using (var stream = new MemoryStream(array))
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(ChunkNumber);
                    writer.Write(ChunkDataTotalSize);
                }
            }
            return array;
        }
        public static ChunkHeader FromStream(BinaryReader reader,Stream stream)
        {
            var chunknum = reader.ReadInt32();
            var chunkdataSize = reader.ReadInt32();
            return new ChunkHeader(chunknum, chunkdataSize);
        }
        public int GetLocalDataPosition(long globalDataPosition)
        {
            if(globalDataPosition<ChunkDataStartPosition || globalDataPosition> ChunkDataEndPosition)
            {
                throw new Exception($"globalDataPosition {globalDataPosition} 不在块范围内[{this.ChunkDataStartPosition},{this.ChunkDataEndPosition}]");
            }
            return (int)(globalDataPosition - ChunkDataStartPosition);
        }
        public override string ToString()
        {
            return $"[ChunkNumber:{ChunkNumber},ChunkDataTotalSize:{ChunkDataTotalSize},ChunkDataStartPosition:{ChunkDataStartPosition},ChunkDataEndPosition:{ChunkDataEndPosition}]";
        }
    }
}
