using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Socketing.BufferManagement
{
    class BufferItemCreator : IPoolItemCreator<byte[]>
    {
        private int _bufferSize;
        public BufferItemCreator(int bufferSize)
        {
            this._bufferSize = bufferSize;
        }
        public IEnumerable<byte[]> Create(int count)
        {
            return new BufferItemEnumerable(_bufferSize, count);
        }
    }
    class BufferItemEnumerable : IEnumerable<byte[]>
    {
        private int _bufferSize;
        private int _count;
        public BufferItemEnumerable(int buferSize,int count)
        {
            this._bufferSize = buferSize;
            this._count = count;
        }
        public IEnumerator<byte[]> GetEnumerator()
        {
            int count = _count;
            for(int i = 0; i < count; i++)
            {
                yield return new byte[_bufferSize];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
           return GetEnumerator();
        }
    }
}
