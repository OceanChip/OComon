using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Storage
{
    public struct RecordWriteResult
    {
        public readonly bool Success;
        public readonly long Position;
        private RecordWriteResult(bool success,long position)
        {
            this.Success = success;
            this.Position = position;
        }
        public static RecordWriteResult NotEnoughSpace()
        {
            return new RecordWriteResult(false, -1);
        }
        public static RecordWriteResult Successful(long position)
        {
            return new RecordWriteResult(true, position);
        }
        public override string ToString()
        {
            return $"[Success:{Success},Position:{Position}]";
        }
    }
}
