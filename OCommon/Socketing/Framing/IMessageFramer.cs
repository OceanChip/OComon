using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Socketing.Framing
{
    public  interface IMessageFramer
    {
        void UnFrameData(IEnumerable<ArraySegment<byte>> data);
        void UnFrameData(ArraySegment<byte> data);
        IEnumerable<ArraySegment<byte>> FrameData(ArraySegment<byte> data);
        void RegisterMessageArrivalCallback(Action<ArraySegment<byte>> handler);
    }
}
