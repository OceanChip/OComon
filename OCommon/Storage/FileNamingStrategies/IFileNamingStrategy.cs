using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Storage.FileNamingStrategies
{
    public interface IFileNamingStrategy
    {
        string GetFileNameFor(string path, int index);
        string[] GetChunkFiles(string path);
        string[] GetTempFiles(string path);
    }
}
