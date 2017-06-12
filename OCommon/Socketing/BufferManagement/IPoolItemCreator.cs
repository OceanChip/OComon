using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Socketing.BufferManagement
{
    public interface  IPoolItemCreator<T>
    {
        IEnumerable<T> Create(int count);
    }
}
