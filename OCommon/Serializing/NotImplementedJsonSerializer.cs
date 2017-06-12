using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Serializing
{
    public class NotImplementedJsonSerializer : IJsonSerializer
    {
        public object Deserialize(string value, Type type)
        {
            throw new NotImplementedException();
        }

        public T Deserialize<T>(string value) where T : class
        {
            throw new NotImplementedException();
        }

        public string Serialize(object obj)
        {
            throw new NotImplementedException();
        }
    }
}
