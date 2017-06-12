using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Storage
{
    public struct RecordBufferReadResult
    {
        public static readonly RecordBufferReadResult Failure = new RecordBufferReadResult(false, null);

        public readonly bool Success;
        public readonly byte[] RecordBuffer;

        public RecordBufferReadResult(bool success,byte[] recordBuffer)
        {
            this.Success = success;
            this.RecordBuffer = recordBuffer;
        }
        public override string ToString()
        {
            return $"[Success:{Success},RecordBufferLength:{RecordBuffer?.Length}";
        }
    }
}
