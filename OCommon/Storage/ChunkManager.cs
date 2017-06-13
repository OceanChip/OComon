using OceanChip.Common.Components;
using OceanChip.Common.Logging;
using OceanChip.Common.Scheduling;
using OceanChip.Common.Utilities;
using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OceanChip.Common.Extensions;

namespace OceanChip.Common.Storage
{
    public class ChunkManager : IDisposable
    {
        private const string ScheduleName = "LogChunkStatisticStatus";
        private const string UncacheChunksScheduleName = "UncacheChunks";
        private static readonly ILogger _loggger = ObjectContainer.Resolve<ILoggerFactory>().Create(typeof(ChunkManager));
        private readonly object _lockObj = new object();
        private readonly ChunkManagerConfig _config;
        private readonly IDictionary<int, Chunk> _chunks;
        private readonly string _chunkPath;
        private IScheduleService _scheduleService;
        private readonly bool _isMemoryMode;
        private int _nextChunkNumber;
        private int _uncachingChunks;
        private int _isCachingNextChunk;
        private ConcurrentDictionary<int, BytesInfo> _bytesWriteDict;
        private ConcurrentDictionary<int, CountInfo> _fileReadDict;
        private ConcurrentDictionary<int, CountInfo> _unmanagedReadDict;
        private ConcurrentDictionary<int, CountInfo> _cachedReadDict;

        class BytesInfo
        {
            public long PreviousBytes;
            public long CurrentBytes;
            public long UpgradeBytes()
            {
                var incrementBytes = CurrentBytes - PreviousBytes;
                PreviousBytes = CurrentBytes;
                return incrementBytes;
            }
        }
        class CountInfo
        {
            public long PreviousCount;
            public long CurrentCount;
            public long UpgradeCount()
            {
                var incrementBytes = CurrentCount - PreviousCount;
                PreviousCount = CurrentCount;
                return incrementBytes;
            }
        }

        public string Name { get; private set; }
        public ChunkManagerConfig Config => _config;
        public string ChunkPath => _chunkPath;
        public bool IsMemoryMode => _isMemoryMode;

        public ChunkManager(string name,ChunkManagerConfig config,bool isMemoryMode,string relativePath = null)
        {
            Check.NotNull(name, nameof(name));
            Check.NotNull(config, nameof(config));

            this.Name = name;
            _config = config;
            _isMemoryMode = isMemoryMode;
            if (string.IsNullOrEmpty(relativePath))
                _chunkPath = _config.BasePath;
            else
                _chunkPath = Path.Combine(_config.BasePath, relativePath);

            if (!Directory.Exists(_chunkPath))
                Directory.CreateDirectory(_chunkPath);

            _chunks = new ConcurrentDictionary<int, Chunk>();
            _scheduleService = ObjectContainer.Resolve<IScheduleService>();
            _bytesWriteDict = new ConcurrentDictionary<int, BytesInfo>();
            _fileReadDict = new ConcurrentDictionary<int, CountInfo>();
            _unmanagedReadDict = new ConcurrentDictionary<int, CountInfo>();
            _cachedReadDict = new ConcurrentDictionary<int, CountInfo>();
        }
        public void Load<T>(Func<byte[],T> readRecordFunc)where T : ILogRecord
        {
            if (_isMemoryMode) return;

            lock (_lockObj)
            {
                if (!Directory.Exists(_chunkPath))
                    Directory.CreateDirectory(_chunkPath);

                var tempFiles = _config.FileNamingStrategy.GetTempFiles(_chunkPath);
                foreach(var file in tempFiles)
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                var files = _config.FileNamingStrategy.GetChunkFiles(_chunkPath);
                if (files.Length > 0)
                {
                    var cachedChunkCount = 0;
                    for(var i = files.Length - 2; i >= 0; i--)
                    {
                        var file = files[i];
                        var chunk = Chunk.FromCompletedFile(file, this, _config, _isMemoryMode);
                        if(_config.EnableCache && cachedChunkCount < _config.PreCacheChunkCount)
                        {
                            if (chunk.TryCacheInMemory(false))
                            {
                                cachedChunkCount++;
                            }
                        }
                        AddChunk(chunk);
                    }
                    var lastFile = files[files.Length - 1];
                    AddChunk(Chunk.FromOngoingFile(lastFile, this, _config, readRecordFunc, _isMemoryMode));
                }
                if (_config.EnableCache)
                {
                    _scheduleService.StartTask(UncacheChunksScheduleName, () => UncacheChunks(), 1000, 1000);
                }
            }
        }
        public int GetChunkCount()
        {
            return _chunks.Count;
        }
        public IList<Chunk> GetAllChunks()
        {
            return _chunks.Values.ToList();
        }
        public Chunk AddNewChunk()
        {
            lock (_lockObj)
            {
                var chunkNumber = _nextChunkNumber;
                var chunkFileName = _config.FileNamingStrategy.GetFileNameFor(_chunkPath, chunkNumber);
                var chunk = Chunk.CreateNew(chunkFileName, chunkNumber, this, _config, _isMemoryMode);
                AddChunk(chunk);

                return chunk;
            }
        }
        public Chunk GetFirstChunk()
        {
            lock (_lockObj)
            {
                if (_chunks.Count == 0)
                    AddNewChunk();
                var minChunkNum = _chunks.Keys.Min();
                return _chunks[minChunkNum];
            }
        }
        public Chunk GetLastChunk()
        {
            lock (_lockObj)
            {
                if (_chunks.Count == 0)
                    AddNewChunk();
                return _chunks[_nextChunkNumber-1];
            }
        }
        public int GetChunkNum(long dataPosition)
        {
            return (int)(dataPosition / _config.GetChunkDataSize());
        }
        public Chunk GetChunkFor(long dataPosition)
        {
            var chunkNum = (int)(dataPosition / _config.GetChunkDataSize());
            return GetChunk(chunkNum);
        }
        public Chunk GetChunk(int chunkNum)
        {
            if (_chunks.ContainsKey(chunkNum))
                return _chunks[chunkNum];
            return null;
        }
        public bool RemoveChunk(Chunk chunk)
        {
            lock (_lockObj)
            {
                if (_chunks.Remove(chunk.ChunkHeader.ChunkNumber))
                {
                    try
                    {
                        chunk.Destroy();
                    }catch(Exception ex)
                    {
                        _loggger.Error($"销毁Chunk发生异常", ex);
                    }
                    return true;
                }
                return false;
            }
        }
        public void TryCacheNextChunk(Chunk currentChunk)
        {
            if (!_config.EnableCache) return;

            if(Interlocked.CompareExchange(ref _isCachingNextChunk, 1, 0) == 0)
            {
                try
                {
                    var nextChunkNumber = currentChunk.ChunkHeader.ChunkNumber + 1;
                    var nextChunk = GetChunk(nextChunkNumber);
                    if (nextChunk != null && !nextChunk.IsMemoryChunk && nextChunk.IsCompleted && !nextChunk.HasCacheChunk) {
                        Task.Factory.StartNew(() => nextChunk.TryCacheInMemory(false));
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _isCachingNextChunk, 0);
                }
            }
        }

        public void Start()
        {
            if (_config.EnableChunkStatistic)
            {
                _scheduleService.StartTask(ScheduleName, LogChunkStatisticStatus, 1000, 1000);
            }
        }
        public void ShutDown()
        {
            if (_config.EnableChunkStatistic)
            {
                _scheduleService.StopTask(ScheduleName);
            }
        }
        private void AddChunk(Chunk chunk)
        {
            _chunks.Add(chunk.ChunkHeader.ChunkNumber, chunk);
            _nextChunkNumber = chunk.ChunkHeader.ChunkNumber + 1;
        }


        public void AddFileReadCount(int chunkNumber)
        {
            _fileReadDict.AddOrUpdate(chunkNumber, GetDefaultCountInfo, UpdateCountInfo);
        }

        public void AddUnmanagedReadCount(int chunkNumber)
        {
            _unmanagedReadDict.AddOrUpdate(chunkNumber, GetDefaultCountInfo, UpdateCountInfo);
        }

        public void AddCachedReadCount(int chunkNumber)
        {
            _cachedReadDict.AddOrUpdate(chunkNumber, GetDefaultCountInfo, UpdateCountInfo);
        }

        public void AddWriteBytes(int chunkNumber, int length)
        {
            _bytesWriteDict.AddOrUpdate(chunkNumber, GetDefaultBytesInfo, (number, current) => UpdateBytesInfo(number, current, length));
        }

        public void Dispose()
        {
            Close();
        }
        public void Close()
        {
            lock (_lockObj)
            {
                _scheduleService.StopTask(UncacheChunksScheduleName);

                foreach(var chunk in _chunks.Values)
                {
                    try
                    {
                        chunk.Close();
                    }catch(Exception ex)
                    {
                        _loggger.Error($"Chunk{chunk}关闭失败", ex);
                    }
                }
            }
        }

        private int UncacheChunks(int maxUncacheCount = 10)
        {
            var uncachedCount = 0;
            if(Interlocked.CompareExchange(ref _uncachingChunks, 1, 0) == 0)
            {
                try
                {
                    var usedMemoryPercent = ChunkUtil.GetUserMemoryPercent();
                    if (usedMemoryPercent <= (ulong)_config.ChunkCacheMinPercent)
                        return 0;

                    if (_loggger.IsDebugEnabled)
                    {
                        _loggger.Debug($"当前内存使用{usedMemoryPercent}%大于配置[chunkCacheMinPercent]{_config.ChunkCacheMinPercent}%,尝试是否缓存");
                    }

                    var chunks = _chunks.Values.Where(p => p.IsCompleted && !p.IsMemoryChunk && p.HasCacheChunk).OrderBy(p => p.LastAtiveTime).ToList();

                    foreach(var chunk in chunks)
                    {
                        if ((DateTime.Now - chunk.LastAtiveTime).TotalSeconds >= _config.ChunkInactiveTimeMaxSeconds)
                        {
                            if (chunk.UnCacheFromMemory())
                            {
                                //即便有了内存释放，由于通过API获取到的内存使用数可能不会立即更新，所以需要等待一段时间后检查内存是否足够
                                Thread.Sleep(100);
                                if (uncachedCount >= maxUncacheCount || ChunkUtil.GetUserMemoryPercent() <= (ulong)_config.ChunkCacheMinPercent)
                                    break;
                            }
                        }
                    }

                    if (_loggger.IsDebugEnabled)
                    {
                        if (uncachedCount > 0)
                            _loggger.Debug($"释放Chunk数量{uncachedCount},当前内存占用{ChunkUtil.GetUserMemoryPercent()}%");
                        else
                            _loggger.Debug("未释放Chunk");
                    }
                }catch(Exception ex)
                {
                    _loggger.Error("释放内存发生异常。", ex);
                }
                finally
                {
                    Interlocked.Exchange(ref _uncachingChunks, 0);
                }
            }
            return uncachedCount;
        }
        private CountInfo GetDefaultCountInfo(int chunkNumber)
        {
            return new CountInfo { CurrentCount = 1 };
        }
        private CountInfo UpdateCountInfo(int chunkNumber,CountInfo info)
        {
            Interlocked.Increment(ref info.CurrentCount);
            return info;
        }
        private BytesInfo GetDefaultBytesInfo(int chunkNumber)
        {
            return new BytesInfo();
        }
        private BytesInfo UpdateBytesInfo(int chunkNumber,BytesInfo bytesInfo,int bytesAdd)
        {
            Interlocked.Add(ref bytesInfo.CurrentBytes, bytesAdd);
            return bytesInfo;
        }
        private void LogChunkStatisticStatus()
        {
            if (_loggger.IsDebugEnabled)
            {
                var bytesWriteStatus = UpdateWriteStatus(_bytesWriteDict);
                var unManagedReadStatus = UpdateReadStatus(_unmanagedReadDict);
                var fileReadStatus = UpdateReadStatus(_fileReadDict);
                var cachedReadStatus = UpdateReadStatus(_cachedReadDict);
                _loggger.Debug($"{Name},maxChunk:#{GetLastChunk().ChunkHeader.ChunkNumber},Write:{bytesWriteStatus}," +
                    $"UnManagedCacheRead:{unManagedReadStatus},LocalCacheRead{cachedReadStatus},FileRead:{fileReadStatus}");
            }
        }

        private string UpdateReadStatus(ConcurrentDictionary<int, CountInfo> dict)
        {
            var list = new List<string>();
            foreach(var entry in dict)
            {
                var num = entry.Key;
                var throughpuh = entry.Value.UpgradeCount();
                if (throughpuh > 0)
                    list.Add("{" + $"chunk:#{num},count:{throughpuh}");
            }
            return list.Count == 0 ? "[]" : string.Join(",", list);
        }

        private string UpdateWriteStatus(ConcurrentDictionary<int, BytesInfo> dict)
        {
            var list = new List<string>();
            var toRemoveKeys = new List<int>();
            foreach(var entry in dict)
            {
                var num = entry.Key;
                var throughpuh = entry.Value.UpgradeBytes() / 1024;
                if (throughpuh > 0)
                    list.Add("{" + $"chunk:#{num},bytes:{throughpuh}/KB");
                else
                    toRemoveKeys.Add(num);
            }
            foreach(var key in toRemoveKeys)
            {
                _bytesWriteDict.Remove(key);
            }
            return list.Count == 0 ? "[]" : string.Join(",", list);
        }
    }
}