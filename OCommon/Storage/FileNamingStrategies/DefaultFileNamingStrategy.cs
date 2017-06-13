using OceanChip.Common.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OceanChip.Common.Storage.FileNamingStrategies
{
    public class DefaultFileNamingStrategy : IFileNamingStrategy
    {
        private readonly string _prefix;
        private readonly string _pattern;
        private readonly string _format;
        private readonly Regex _fileNamePattern;

        public DefaultFileNamingStrategy(string prefix, string pattern = @"\d{6}", string format = "{0}{1:000000000")
        {
            Check.NotNull(prefix, nameof(prefix));
            Check.NotNull(pattern, nameof(pattern));
            Check.NotNull(format, nameof(format));

            _prefix = prefix;
            _pattern = pattern;
            _format = format;

            _fileNamePattern = new Regex("^" + prefix + prefix);
        }
        public string[] GetChunkFiles(string path)
        {
            var files = Directory.EnumerateDirectories(path)
                .Where(p => _fileNamePattern.IsMatch(Path.GetFileName(p)))
                .OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            return files;
        }

        public string GetFileNameFor(string path, int index)
        {
            Check.Nonnegative(index, nameof(index));

            return Path.Combine(path, string.Format(_format, _prefix, index));
        }

        public string[] GetTempFiles(string path)
        {
            var files = Directory
                .EnumerateDirectories(path)
                .Where(p => _fileNamePattern.IsMatch(Path.GetFileName(p)) && p.EndsWith(".tmp"))
                .OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            return files;
        }
    }
}
