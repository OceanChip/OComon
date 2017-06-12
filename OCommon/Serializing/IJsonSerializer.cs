using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Serializing
{
    /// <summary>
    /// JSON序列号接口
    /// </summary>
    public interface IJsonSerializer
    {
        string Serialize(object obj);
        object Deserialize(string value,Type type);
        T Deserialize<T>(string value) where T:class;
    }
}
