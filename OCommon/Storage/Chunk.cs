using OceanChip.Common.Components;
using OceanChip.Common.Logging;
using OceanChip.Common.Storage.Exceptions;
using OceanChip.Common.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OceanChip.Common.Storage
{
    public unsafe class Chunk:IDisposable
    {
        private static readonly ILogger _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(typeof(Chunk));

        private ChunkHeader _chunkHeader;
        private ChunkFooter _chunkFooter;

        private readonly string _fileName;
        private readonly ChunkManager _chunkManager;
        private readonly ChunkManagerConfig _chunkConfig;
        private readonly bool _isMemoryChunk;
        private readonly ConcurrentQueue<ReaderWorkItem> _readerWorkItemQueue = new ConcurrentQueue<ReaderWorkItem>();

        private readonly object _writeSyncObj = new object();
        private readonly object _cacheSyncObj = new object();
        private readonly object _freeMemoryobj = new object();

        private int _dataPosition;
        private bool _isCompleted;
        private bool _isDestroying;
        private bool _isMemoryFreed;
        private int _cachingChunk;
        private DateTime _lastActiveTime;
        private bool _isReadersInitialized;
        private int _flushedDataPosition;

        private Chunk _memoryChunk;
        private CacheItem[] _cacheItems;
        private IntPtr _cacheData;
        private int _cachedLength;

        private WriterWorkItem _writerWorkItem;

        public string FileName => _fileName;
        public ChunkHeader ChunkHeader => _chunkHeader;
        public ChunkFooter ChunkFooter => _chunkFooter;
        public ChunkManagerConfig Config => _chunkConfig;
        public bool IsCompleted => _isCompleted;
        public DateTime LastAtiveTime
        {
            get
            {
                var lastActiveTimeofMemoryChunk = DateTime.MinValue;
                if (_memoryChunk != null)
                    lastActiveTimeofMemoryChunk = _memoryChunk.LastAtiveTime;
                return lastActiveTimeofMemoryChunk >= _lastActiveTime ? lastActiveTimeofMemoryChunk : _lastActiveTime;
            }
        }
        public bool IsMemoryChunk => _isMemoryChunk;
        public bool HasCacheChunk => _memoryChunk != null;
        public int DataPosition => _dataPosition;
        public long GlobalDataPosition
        {
            get
            {
                return ChunkHeader.ChunkDataStartPosition + DataPosition;
            }
        }
        public bool IsFixedDataSize()
        {
            return _chunkConfig.ChunkDataUnitSize > 0 && _chunkConfig.ChunkDataCount > 0;
        }
#region 构造函数 析构函数
        private Chunk(string fileName,ChunkManager manager,ChunkManagerConfig config,bool isMemoryChunk)
        {
            Check.NotNullOrEmpty(fileName, nameof(fileName));
            Check.NotNull(manager, nameof(manager));
            Check.NotNull(config, nameof(config));

            _fileName = fileName;
            _chunkManager = manager;
            _chunkConfig = config;
            _isMemoryChunk = isMemoryChunk;
            _lastActiveTime = DateTime.Now;
        }
        ~Chunk()
        {
            UnCacheFromMemory();
        }
#endregion
        class CacheItem
        {
            public long RecordPosition;
            public byte[] RecordBuffer;
        }
        class ChunkFileStream : IStream
        {
            public Stream Stream;
            public FlushOption FlushOption;
            public ChunkFileStream(Stream stream,FlushOption option)
            {
                this.Stream = stream;
                this.FlushOption = option;
            }
            public long Length => Stream.Length;

            public long Position { get =>Stream.Position; set => Stream.Position=value; }

            public void Dispose()
            {
                Stream.Dispose();
            }

            public void Flush()
            {
                var fileStream = Stream as FileStream;
                if (fileStream != null)
                {
                    if (FlushOption == FlushOption.FlushToDisk)
                        fileStream.Flush(true);
                    else
                        fileStream.Flush();
                }
                else
                    Stream.Flush();
            }

            public void SetLength(long value)
            {
                Stream.SetLength(value);
            }

            public void Write(byte[] buffer, int offset, int count)
            {
                Stream.Write(buffer, offset, count);
            }
        }

        public static Chunk FromCompletedFile(string fileName, ChunkManager chunkManager, ChunkManagerConfig config, bool isMemoryMode)
        {
            var chunk = new Chunk(fileName, chunkManager, config, isMemoryMode);
            try
            {
                chunk.InitCompleted();
            }
            catch (OutOfMemoryException)
            {
                chunk.Dispose();
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error($"Chunk {chunk}初始化文件处理失败", ex);
            }
            return chunk;
        }

        private void InitCompleted()
        {
            var fileInfo = new FileInfo(_fileName);
            if (!fileInfo.Exists)
                throw new ChunkFileNotExistException(_fileName);
            _isCompleted = true;

            using(var fileStream=new FileStream(_fileName,FileMode.Open,FileAccess.Read,FileShare.ReadWrite,
                _chunkConfig.ChunkReadBuffer, FileOptions.None))
            {
                using (var reader = new BinaryReader(fileStream))
                {
                    _chunkHeader = ReadHeader(fileStream, reader);
                    _chunkFooter = ReadFooter(fileStream, reader);

                    CheckCompletedFileChunk();
                }
            }

            _dataPosition = _chunkFooter.ChunkDataTotalSize;
            _flushedDataPosition = _chunkFooter.ChunkDataTotalSize;

            if (_isMemoryChunk)
                LoadFileChunkToMemory();
            else
                SetFileAttributes();

            InitializeReaderWorkItems();
        }

        private void InitializeReaderWorkItems()
        {
            for(int i = 0; i < _chunkConfig.ChunkReaderCount; i++)
            {
                _readerWorkItemQueue.Enqueue(CreateReaderWorkItem());
            }
            _isReadersInitialized = true;
        }
        /// <summary>
        /// 
        /// </summary>
        private void CloseAllReaderWorkItems()
        {
            if (!_isReadersInitialized) return;
            var watch = Stopwatch.StartNew();
            var closeCount = 0;

            while (closeCount < _chunkConfig.ChunkReaderCount)
            {
                ReaderWorkItem readerWorkItem;
                while(_readerWorkItemQueue.TryDequeue(out readerWorkItem))
                {
                    readerWorkItem.Reader.Close();
                    closeCount++;
                }
                Thread.Sleep(1000);
                if (watch.ElapsedMilliseconds > 30 * 1000)
                {
                    _logger.Error($"关闭ReaderWorkItem超时，总数量：{_chunkConfig.ChunkReaderCount},实际关闭数量：{closeCount}");
                    break;
                }
            }
        }
        private ReaderWorkItem CreateReaderWorkItem()
        {
            var stream = default(Stream);
            if (_isMemoryChunk)
            {
                stream = new UnmanagedMemoryStream((byte*)_cacheData, _cachedLength);
            }
            else
            {
                stream = new FileStream(_fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, _chunkConfig.ChunkReadBuffer, FileOptions.None);
            }
            return new ReaderWorkItem(stream, new BinaryReader(stream));
        }
        private void SetFileAttributes()
        {
            Helper.ExecuteActionWithoutException(() => File.SetAttributes(_fileName, FileAttributes.NotContentIndexed));
        }
        /// <summary>
        /// 加载文件中的Chunk到内存
        /// </summary>
        private void LoadFileChunkToMemory()
        {
            using (var fs = new FileStream(_fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 8192, FileOptions.None))
            {
                var cachedLength = (int)fs.Length;
                var cachedData = Marshal.AllocHGlobal(cachedLength);

                try
                {
                    using(var unmanagedStream=new UnmanagedMemoryStream((byte*)cachedData,cachedLength,cachedLength, FileAccess.ReadWrite))
                    {
                        fs.Seek(0, SeekOrigin.Begin);
                        var buffer = new byte[65535];
                        var toRead = cachedLength;
                        while (toRead > 0)
                        {
                            int read = fs.Read(buffer, 0, Math.Min(toRead, buffer.Length));
                            if (read == 0)
                                break;
                            toRead -= read;
                            unmanagedStream.Write(buffer, 0, read);
                        }
                    }
                }
                catch
                {
                    Marshal.FreeHGlobal(cachedData);
                    throw;
                }
                _cacheData = cachedData;
                _cachedLength = cachedLength;
            }
        }

        private void CheckCompletedFileChunk()
        {
            using (var fs = new FileStream(_fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, _chunkConfig.ChunkReadBuffer, FileOptions.None))
            {
                //检查chunk文件的实际大小是否正确
                var chunkFileSize = ChunkHeader.Size + _chunkConfig.ChunkDataSize + ChunkFooter.Size;
                if (chunkFileSize != fs.Length)
                    throw new ChunkBadDataException($"Chunk[{this}] 长度：{chunkFileSize},文件长度：{fs.Length}");
                //是否是固定大小，则还需要检查总数是否正确
                if (IsFixedDataSize())
                {
                    if (_chunkFooter.ChunkDataTotalSize != ChunkHeader.ChunkDataTotalSize)
                        throw new ChunkBadDataException($"固定Chunk[{this}],总长度：{_chunkHeader.ChunkDataTotalSize},实际：{_chunkFooter.ChunkDataTotalSize}");
                }
            }
        }

        private ChunkFooter ReadFooter(FileStream fileStream, BinaryReader reader)
        {
            if (fileStream.Length < ChunkFooter.Size)
                throw new Exception($"Chunk文件{_fileName}长度比已定义的Chunk尾的长度短，文件：{fileStream.Length},Chunk头：{ChunkHeader.Size}");
            fileStream.Seek(0, SeekOrigin.Begin);
            return ChunkFooter.FromStream(reader, fileStream);
        }

        private ChunkHeader ReadHeader(FileStream fileStream, BinaryReader reader)
        {
            if (fileStream.Length < ChunkHeader.Size)
                throw new Exception($"Chunk文件{_fileName}长度比已定义的Chunk头的长度短，文件：{fileStream.Length},Chunk头：{ChunkHeader.Size}");
            fileStream.Seek(0, SeekOrigin.Begin);
            return ChunkHeader.FromStream(reader, fileStream);
        }

        public bool TryCacheInMemory(bool shoudCacheNextChunk)
        {
            lock (_cacheSyncObj)
            {
                if(!_chunkConfig.EnableCache || _isMemoryChunk || !_isCompleted || _memoryChunk != null)
                {
                    _cachingChunk = 0;
                    return false;
                }

                try
                {
                    var chunkSize = (ulong)GetChunkSize(_chunkHeader);
                    if (!ChunkUtil.IsMemoryEnoughToCacheChunk(chunkSize, (uint)_chunkConfig.ChunkCacheMaxPercent))
                    {
                        return false;
                    }
                    _memoryChunk = FromCompletedFile(_fileName, _chunkManager, _chunkConfig, true);
                    if (shoudCacheNextChunk)
                    {
                        Task.Factory.StartNew(() => _chunkManager.TryCacheNextChunk(this));
                    }
                    return true;
                }
                catch (OutOfMemoryException) { return false; }
                catch(Exception ex)
                {
                    _logger.Error($"缓存数据块失败 {this}", ex);
                    return false;
                }
                finally
                {
                    _cachingChunk = 0;
                }
            }
        }

        private int GetChunkSize(ChunkHeader chunkHeader)
        {
            return ChunkHeader.Size + chunkHeader.ChunkDataTotalSize + ChunkFooter.Size;
        }

        internal static Chunk FromOngoingFile<T>(string fileName, ChunkManager chunkManager, ChunkManagerConfig config, Func<byte[], T> readRecordFunc, bool isMemoryMode) where T : ILogRecord
        {
            var chunk = new Chunk(fileName, chunkManager, config, isMemoryMode);
            try
            {
                chunk.InitOngoing(readRecordFunc);
            }
            catch (OutOfMemoryException)
            {
                chunk.Dispose();
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error($"Chunk {chunk}初始化文件处理失败", ex);
            }
            return chunk;
        }

        private void InitOngoing<T>(Func<byte[], T> readRecordFunc) where T : ILogRecord
        {
            var fileInfo = new FileInfo(_fileName);
            if (!fileInfo.Exists)
            {
                throw new ChunkFileNotExistException(_fileName);
            }
            _isCompleted = false;

            var writeStream = default(Stream);

            if (_isMemoryChunk)
            {
                var fileSize = ChunkHeader.Size + ChunkHeader.ChunkDataTotalSize + ChunkFooter.Size;
                _cachedLength = fileSize;
                _cacheData = Marshal.AllocHGlobal(_cachedLength);
                writeStream = new UnmanagedMemoryStream((byte*)_cacheData, _cachedLength, _cachedLength, FileAccess.ReadWrite);

                writeStream.Write(_chunkHeader.AsByteArray(), 0, ChunkHeader.Size);

                if (_dataPosition > 0)
                {
                    using (var fstream = new FileStream(_fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 8192, FileOptions.SequentialScan))
                    {
                        fstream.Seek(ChunkHeader.Size, SeekOrigin.Begin);
                        var buffer = new byte[65535];
                        int toReadBytes = _dataPosition;

                        while (toReadBytes > 0)
                        {
                            int read = fstream.Read(buffer, 0, Math.Min(toReadBytes, buffer.Length));
                            if (read == 0)
                                break;
                            toReadBytes -= read;
                            writeStream.Write(buffer, 0, read);
                        }
                    }
                }
                if(writeStream.Position != GetStreamPosition(_dataPosition))
                {
                    throw new InvalidOperationException($"UnmanagedMemoryStream位置不正确，期望：{_dataPosition+ChunkHeader.Size},实际：{writeStream.Position}");
                }
                else
                {
                    writeStream = new FileStream(_fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, _chunkConfig.ChunkWriteBuffer, FileOptions.SequentialScan);
                    writeStream.Position = GetStreamPosition(_dataPosition);
                    SetFileAttributes();
                }

                _writerWorkItem = new WriterWorkItem(new ChunkFileStream(writeStream, _chunkConfig.FlushOption));

                InitializeReaderWorkItems();

                if (!_isMemoryChunk)
                {
                    if (_chunkConfig.EnableCache)
                    {
                        var chunkSize = (ulong)GetChunkSize(_chunkHeader);
                        if (ChunkUtil.IsMemoryEnoughToCacheChunk(chunkSize, (uint)_chunkConfig.ChunkCacheMaxPercent))
                        {
                            try
                            {
                                _memoryChunk = FromOngoingFile(_fileName, _chunkManager, _chunkConfig,readRecordFunc, true);
                            }
                            catch (OutOfMemoryException)
                            {
                                _cacheItems = new CacheItem[_chunkConfig.ChunkLocalCacheSize];
                            }
                            catch (Exception ex)
                            {
                                _logger.Error($"无法缓存块{this}", ex);
                                _cacheItems = new CacheItem[_chunkConfig.ChunkLocalCacheSize];
                            }
                        }
                        else
                        {
                            _cacheItems = new CacheItem[_chunkConfig.ChunkLocalCacheSize];
                        }
                    }
                    else
                    {
                        _cacheItems = new CacheItem[_chunkConfig.ChunkLocalCacheSize];
                    }
                }
                _lastActiveTime = DateTime.Now;
                if (!_isMemoryChunk)
                {
                    _logger.Info($"连续块{this}初始化完成，_dataPosition:{_dataPosition}");
                }
            }
        }

        private long GetStreamPosition(long dataPosition)
        {
            return ChunkHeader.Size + dataPosition;
        }
        /// <summary>
        /// 新增
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="chunkNumber"></param>
        /// <param name="chunkManager"></param>
        /// <param name="config"></param>
        /// <param name="isMemoryMode"></param>
        /// <returns></returns>
        public static Chunk CreateNew(string fileName, int chunkNumber, ChunkManager chunkManager, ChunkManagerConfig config, bool isMemoryMode)
        {
            var chunk = new Chunk(fileName, chunkManager, config, isMemoryMode);
            try
            {
                chunk.InitNew(chunkNumber);
            }
            catch (OutOfMemoryException)
            {
                chunk.Dispose();
                throw;
            }
            catch(Exception ex)
            {
                _logger.Error($"Chunk {chunk}创建失败", ex);
            }
            return chunk;
        }
        /// <summary>
        /// 新增新块
        /// </summary>
        /// <param name="chunkNumber"></param>
        private void InitNew(int chunkNumber)
        {
            var chunkDataSize = 0;
            if (_chunkConfig.ChunkDataSize > 0)
                chunkDataSize = _chunkConfig.ChunkDataSize;
            else
                chunkDataSize = _chunkConfig.ChunkDataUnitSize * _chunkConfig.ChunkDataSize;
            _chunkHeader = new ChunkHeader(chunkNumber, chunkDataSize);
            _isCompleted = false;

            var fileSize = ChunkHeader.Size + ChunkHeader.ChunkDataTotalSize + ChunkFooter.Size;

            var writeStream = default(Stream);
            var tempFilename = $"{_fileName}.{Guid.NewGuid()}.tmp";
            var tempFileStream = default(FileStream);

            try
            {
                if (_isMemoryChunk)
                {
                    _cachedLength = fileSize;
                    _cacheData = Marshal.AllocHGlobal(_cachedLength);
                    writeStream = new UnmanagedMemoryStream((byte*)_cacheData, _cachedLength, _cachedLength, FileAccess.ReadWrite);
                    writeStream.Write(_chunkHeader.AsByteArray(), 0, ChunkHeader.Size);
                }
                else
                {
                    var fileInfo = new FileInfo(_fileName);
                    if (fileInfo.Exists)
                    {
                        File.SetAttributes(_fileName, FileAttributes.Normal);
                        File.Delete(_fileName);
                    }

                    tempFileStream = new FileStream(tempFilename, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, _chunkConfig.ChunkWriteBuffer, FileOptions.None);
                    tempFileStream.SetLength(fileSize);
                    tempFileStream.Write(_chunkHeader.AsByteArray(), 0, ChunkHeader.Size);
                    tempFileStream.Flush(true);
                    tempFileStream.Close();

                    File.Move(tempFilename, _fileName);

                    writeStream = new FileStream(_fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, _chunkConfig.ChunkWriteBuffer, FileOptions.SequentialScan);
                    SetFileAttributes();
                }
                writeStream.Position = ChunkHeader.Size;

                _dataPosition = 0;
                _flushedDataPosition = 0;
                _writerWorkItem = new WriterWorkItem(new ChunkFileStream(writeStream, _chunkConfig.FlushOption));

                InitializeReaderWorkItems();

                if (!IsMemoryChunk)
                {
                    if (Config.EnableCache)
                    {
                        var chunkSize = (ulong)GetChunkSize(_chunkHeader);
                        if (ChunkUtil.IsMemoryEnoughToCacheChunk(chunkSize, (uint)_chunkConfig.ChunkCacheMaxPercent))
                        {
                            try
                            {
                                _memoryChunk = CreateNew(_fileName, chunkNumber, _chunkManager, _chunkConfig, true);
                            }
                            catch (OutOfMemoryException)
                            {
                                _cacheItems = new CacheItem[_chunkConfig.ChunkLocalCacheSize];
                            }catch(Exception ex)
                            {
                                _logger.Error($"缓存新Chunk失败", ex);
                                _cacheItems = new CacheItem[_chunkConfig.ChunkLocalCacheSize];
                            }
                        }
                        else
                        {
                            _cacheItems = new CacheItem[_chunkConfig.ChunkLocalCacheSize];
                        }
                    }
                    else
                    {
                        _cacheItems = new CacheItem[_chunkConfig.ChunkLocalCacheSize];
                    }
                }
            }
            catch
            {
                if (!IsMemoryChunk)
                {
                    if(tempFileStream != null){
                        Helper.ExecuteActionWithoutException(() => tempFileStream.Close());
                    }
                    if (File.Exists(tempFilename))
                    {
                        Helper.ExecuteActionWithoutException(() =>
                        {
                            File.SetAttributes(tempFilename, FileAttributes.Normal);
                            File.Delete(tempFilename);
                        });
                    }
                }
                throw;
            }
            _lastActiveTime = DateTime.Now;
        }

        public void Destroy()
        {
            if (_isMemoryChunk)
            {
                FreeMemory();
                return;
            }
            //检查当前chunk是否已经完成
            if (!_isCompleted)
            {
                throw new InvalidOperationException($"不允许清除未处理完成的Chunk{this}");
            }

            _isDestroying = true;
            if (_cacheItems != null)
                _cacheItems = null;
            UnCacheFromMemory();
            CloseAllReaderWorkItems();
            File.SetAttributes(_fileName, FileAttributes.Normal);
            File.Delete(_fileName);
        }

        private void FreeMemory()
        {
            if(_isMemoryChunk && !_isMemoryFreed)
            {
                lock (_freeMemoryobj)
                {
                    var cachedData = Interlocked.Exchange(ref _cacheData, IntPtr.Zero);
                    if(cachedData!= IntPtr.Zero)
                    {
                        try
                        {
                            Marshal.FreeHGlobal(cachedData);
                        }
                        catch(Exception ex)
                        {
                            _logger.Error($"释放内存Chunk[{this}]失败", ex);
                        }
                    }
                    _isMemoryFreed = true;
                }
            }
        }


        /// <summary>
        /// 清除缓存
        /// </summary>
        /// <returns></returns>
        public bool UnCacheFromMemory()
        {
            lock (_cacheSyncObj)
            {
                if (!_chunkConfig.EnableCache || _isMemoryChunk || !_isCompleted || _memoryChunk == null)
                    return false;

                try
                {
                    var memoryChunk = _memoryChunk;
                    _memoryChunk = null;
                    memoryChunk.Dispose();
                    return true;
                }catch(Exception ex)
                {
                    _logger.Error($"清除缓存失败：{this}", ex);
                    return false;
                }
            }
        }
        public T TryReadAt<T>(long dataPosition,Func<byte[],T> readRecordFunc,bool autoCache=true)where T : class, ILogRecord
        {
            if (_isDestroying)
                throw new ChunkReadException($"数据块{this}正在开始删除");

            _lastActiveTime = DateTime.Now;

            if (!_isMemoryChunk)
            {
                if (_cacheItems != null)
                {
                    var index = dataPosition * _chunkConfig.ChunkLocalCacheSize;
                    var cacheItem = _cacheItems[index];
                    if(cacheItem !=null && cacheItem.RecordPosition == dataPosition)
                    {
                        var record = readRecordFunc(cacheItem.RecordBuffer);
                        if (record == null)
                            throw new ChunkReadException($"无法读取数据位置：{_dataPosition}.数据块中发生严重错误：{this}");

                        if (_chunkConfig.EnableChunkStatistic)
                        {
                            _chunkManager.AddCachedReadCount(ChunkHeader.ChunkNumber);
                        }
                        return record;
                    }
                }else if (_memoryChunk != null)
                {
                    var record = _memoryChunk.TryReadAt(dataPosition, readRecordFunc);
                    if(record !=null && _chunkConfig.EnableChunkStatistic)
                    {
                        _chunkManager.AddUnmanagedReadCount(ChunkHeader.ChunkNumber);
                    }
                    return record;
                }
            }
            if(_chunkConfig.EnableCache && autoCache && !_isMemoryChunk && _isCompleted &&
                Interlocked.CompareExchange(ref _cachingChunk, 1, 0) == 0)
            {
                Task.Factory.StartNew(() => TryCacheInMemory(true));
            }

            var readerWorkItem = GetReaderWorkItem();
            try
            {
                var currentDataPosition = DataPosition;
                if (dataPosition >= currentDataPosition)
                    return null;

                try
                {
                    var record = IsFixedDataSize() ?
                        TryReadFixedSizeForwardInternal(readerWorkItem, dataPosition, readRecordFunc) :
                        TryReadForwardInternal(readerWorkItem, dataPosition, readRecordFunc);
                    if(!_isMemoryChunk && _chunkConfig.EnableChunkStatistic)
                    {
                        _chunkManager.AddFileReadCount(ChunkHeader.ChunkNumber);
                    }
                    return record;
                }
                catch
                {
                    if (!_isMemoryChunk && _writerWorkItem != null && _writerWorkItem.LastFlushPosition < GetStreamPosition(_dataPosition))
                        return null;
                    else
                        throw;
                }
            }
            finally
            {
                ReturnReaderWorkItem(readerWorkItem);
            }
        }
        public RecordWriteResult TryAppend(ILogRecord record)
        {
            if (_isCompleted)
            {
                throw new ChunkWriteException(this.ToString(), $"无法向自渎块中写内容，isMemoryChunk：{_isMemoryChunk},_dataPosition:{_dataPosition}");
            }
            _lastActiveTime = DateTime.Now;

            var writerWorkItem = _writerWorkItem;
            var bufferStream = writerWorkItem.BufferStream;
            var bufferWriter = writerWorkItem.BufferWriter;
            var recordBuffer = default(byte[]);

            if (IsFixedDataSize())
            {
                if (writerWorkItem.WorkingStream.Position + _chunkConfig.ChunkDataUnitSize > ChunkHeader.Size + ChunkHeader.ChunkDataTotalSize)
                {
                    return RecordWriteResult.NotEnoughSpace();
                }
                bufferStream.Position = 0;
                record.WriteTo(GlobalDataPosition, bufferWriter);
                var recordLength = (int)bufferStream.Length;
                if (recordLength != _chunkConfig.ChunkDataUnitSize)
                {
                    throw new ChunkWriteException(this.ToString(), $"无效固定长度，期望：{_chunkConfig.ChunkDataUnitSize},实际：{recordLength}");
                }
                if (_cacheItems != null)
                {
                    recordBuffer = new byte[recordLength];
                    Buffer.BlockCopy(bufferStream.GetBuffer(), 0, recordBuffer, 0, recordLength);
                }
            }
            else
            {
                bufferStream.SetLength(4);
                bufferStream.Position = 4;
                record.WriteTo(GlobalDataPosition, bufferWriter);
                var recordLength = (int)bufferStream.Length - 4;
                bufferWriter.Write(recordLength);
                bufferStream.Position = 0;
                bufferWriter.Write(recordLength);

                if (recordLength > _chunkConfig.MaxLogRecordSize)
                    throw new ChunkWriteException(this.ToString(), $"记录从位置{_dataPosition}开始长度太长:{recordLength}字节,而数据限制为：{_chunkConfig.MaxLogRecordSize}");
                if (writerWorkItem.WorkingStream.Position + recordLength + 2 * sizeof(int) > ChunkHeader.Size + ChunkHeader.ChunkDataTotalSize)
                    return RecordWriteResult.NotEnoughSpace();

                if (_cacheItems != null)
                {
                    recordBuffer = new byte[recordLength];
                    Buffer.BlockCopy(bufferStream.GetBuffer(), 4, recordBuffer, 0, recordLength);
                }
            }
            var writtenPosition = _dataPosition;
            var buffer = bufferStream.GetBuffer();

            lock (_writeSyncObj)
            {
                writerWorkItem.AppendData(buffer, 0, (int)bufferStream.Length);
            }
            var position = (int)writerWorkItem.WorkingStream.Position + writtenPosition;

            if (_chunkConfig.EnableCache)
            {
                if (_memoryChunk != null)
                {
                    var result = _memoryChunk.TryAppend(record);
                    if (!result.Success)
                    {
                        throw new ChunkWriteException(this.ToString(), $"追加记录成功，但是缓存到内存中失败，可能是由于内存不足引起的。");
                    }else if (result.Position != position)
                    {
                        throw new ChunkWriteException(this.ToString(), $"追加记录与缓存成功，但是缓存位置与实际不相符，内存：{result.Position},文件：{position}");
                    }
                }else if(_cacheItems!=null && recordBuffer != null)
                {
                    var index = writtenPosition % _chunkConfig.ChunkLocalCacheSize;
                    _cacheItems[index] = new CacheItem { RecordPosition = writtenPosition, RecordBuffer = recordBuffer };
                }
            }
            else if (_cacheItems != null && recordBuffer != null)
            {
                var index = writtenPosition % _chunkConfig.ChunkLocalCacheSize;
                _cacheItems[index] = new CacheItem { RecordPosition = writtenPosition, RecordBuffer = recordBuffer };
            }
            if(!_isMemoryChunk && _chunkConfig.EnableChunkStatistic)
            {
                _chunkManager.AddWriteBytes(ChunkHeader.ChunkNumber, (int)bufferStream.Length);
            }
            return RecordWriteResult.Successful(position);
        }
        public void Flush()
        {
            if (_isMemoryChunk || _isCompleted) return;
            if (_writerWorkItem != null)
                Helper.ExecuteActionWithoutException(() => _writerWorkItem.FlushToDisk());
        }
        public void Complete()
        {
            lock (_writeSyncObj)
            {
                if (_isCompleted) return;

                _chunkFooter = WriteFooter();
                if (!_isMemoryChunk)
                {
                    Flush();
                }
                SetFileAttributes();
                if (_memoryChunk != null)
                {
                    _memoryChunk.Complete();
                }
            }
        }
        private ReaderWorkItem GetReaderWorkItem()
        {
            ReaderWorkItem readerWorkItem;
            while(!_readerWorkItemQueue.TryDequeue(out readerWorkItem))
            {
                Thread.Sleep(1);
            }
            return readerWorkItem;
        }

        private T TryReadForwardInternal<T>(ReaderWorkItem readerWorkItem, long dataPosition, Func<byte[], T>  readRecordFunc) where T:class,ILogRecord
        {
            lock (_freeMemoryobj)
            {
                if (_isMemoryFreed)
                    return default(T);

                var currentDataPosition = DataPosition;

                if (dataPosition + 2 * sizeof(int) > currentDataPosition)
                {
                    throw new ChunkReadException($"没有足够的空间分配给前缀与后缀,数据位置：{dataPosition},最大：{currentDataPosition},Chunk:{this}");
                }
                readerWorkItem.Stream.Position = GetStreamPosition(dataPosition);
                var length = readerWorkItem.Reader.ReadInt32();
                if (length <= 0)
                {
                    throw new ChunkReadException($"Chunk[{this}]记录在位置{dataPosition}读取不到长度:{length}");
                }
                if (length >= _chunkConfig.MaxLogRecordSize)
                {
                    throw new ChunkReadException($"Chunk[{this}]记录位置{dataPosition}的长度({length})比限制值（{_chunkConfig.MaxLogRecordSize})大");
                }
                if (dataPosition+length + 2 * sizeof(int) > currentDataPosition)
                {
                    throw new ChunkReadException($"无足够内存空间读取完整记录（前缀长度:{length},位置：{dataPosition},最大：{currentDataPosition},Chunk:{this})");
                }
                var recordBuffer = readerWorkItem.Reader.ReadBytes(length);
                var record = readRecordFunc(recordBuffer);
                if (record != null)
                    throw new ChunkReadException($"无法从{dataPosition}读取记录。Chunk({this})发生严重错误");

                int suffixLength = readerWorkItem.Reader.ReadInt32();
                if (suffixLength != length)
                    throw new ChunkReadException($"Chunk({this})前后缀长度不一致,前缀长度:{length},后缀:{suffixLength},位置：{dataPosition}");

                return record;
            }            
        }

        private T TryReadFixedSizeForwardInternal<T>(ReaderWorkItem readerWorkItem, long dataPosition, Func<byte[], T> readRecordFunc) where T : class, ILogRecord
        {
            lock (_freeMemoryobj)
            {
                if (_isMemoryFreed)
                    return default(T);

                var currentDataPosition = DataPosition;

                if (dataPosition + _chunkConfig.ChunkDataUnitSize > currentDataPosition)
                {
                    throw new ChunkReadException($"没有足够的空间分配给前缀与后缀,数据位置：{dataPosition},最大：{currentDataPosition},Chunk:{this}");
                }
                var startPosition= GetStreamPosition(dataPosition);
                readerWorkItem.Stream.Position = startPosition;
                
                var recordBuffer = readerWorkItem.Reader.ReadBytes(_chunkConfig.ChunkDataUnitSize);
                var record = readRecordFunc(recordBuffer);
                if (record != null)
                    throw new ChunkReadException($"无法从{dataPosition}读取固定长度记录。Chunk({this})发生严重错误");

                var recordLength = readerWorkItem.Stream.Position - startPosition;
                if (recordLength != _chunkConfig.ChunkDataUnitSize)
                    throw new ChunkReadException($"Chunk({this})固定记录的长度无效,长度：{_chunkConfig.ChunkDataUnitSize},实际长度:{recordLength},位置：{dataPosition}");
                
                return record;
            }
        }
        private bool TryParsingDataPosition<T>(Func<byte[],T> readRecordFunc,out ChunkHeader header,out int dataPosition)where T : ILogRecord
        {
            using (var fs = new FileStream(_fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, _chunkConfig.ChunkReadBuffer, FileOptions.None))
            {
                using (var reader = new BinaryReader(fs))
                {
                    header = ReadHeader(fs, reader);

                    fs.Position = ChunkHeader.Size;

                    var startPostion = fs.Position;
                    var maxPosition = fs.Length - ChunkFooter.Size;
                    var isFixedDataSize = IsFixedDataSize();

                    while (fs.Position < maxPosition)
                    {
                        var success = false;
                        if (isFixedDataSize)
                        {
                            success = TryReadFixedRecord(fs, reader, maxPosition, readRecordFunc);
                        }
                        else
                        {
                            success = TryReadRecord(fs, reader, maxPosition, readRecordFunc);
                        }
                        if (success)
                            startPostion = fs.Position;
                        else
                            break;
                    }
                    if (startPostion != fs.Length)
                        fs.Position = startPostion;
                    dataPosition = (int)fs.Position - ChunkHeader.Size;
                    return true;
                }
            }
        }

        private bool TryReadRecord<T>(FileStream fs, BinaryReader reader, long maxPosition, Func<byte[], T> readRecordFunc) where T : ILogRecord
        {
            try
            {
                var startPosition = fs.Position;
                if (startPosition + 2 * sizeof(int) > maxPosition)
                {
                    return false;
                }

                var length = reader.ReadInt32();
                if (length <= 0 || length > _chunkConfig.MaxLogRecordSize)
                    return false;

                if (startPosition + length + 2 * sizeof(int) > maxPosition)
                    return false;

                var recordBuffer = reader.ReadBytes(length);
                var record = readRecordFunc(recordBuffer);
                if (record == null)
                    return false;
                int suffixLength = reader.ReadInt32();
                if (suffixLength != length)
                    return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TryReadFixedRecord<T>(FileStream fs, BinaryReader reader, long maxPosition, Func<byte[], T> readRecordFunc) where T : ILogRecord
        {
            try
            {
                var startPositon = fs.Position;
                if (startPositon + _chunkConfig.ChunkDataUnitSize > maxPosition)
                    return false;

                var recordBuffer = reader.ReadBytes(_chunkConfig.ChunkDataUnitSize);
                var record = readRecordFunc(recordBuffer);
                if (record == null)
                    return false;
                var recordLength = fs.Position - startPositon;
                if (recordLength != _chunkConfig.ChunkDataUnitSize)
                {
                    _logger.Error($"Chunk({this})固定记录的长度无效,长度：{_chunkConfig.ChunkDataUnitSize},实际长度:{recordLength}");
                    return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ReturnReaderWorkItem(ReaderWorkItem readerWorkItem)
        {
            _readerWorkItemQueue.Enqueue(readerWorkItem);
        }
        private ChunkFooter WriteFooter()
        {
            var currentTotalDataSize = DataPosition;
            if (IsFixedDataSize())
            {
                if (currentTotalDataSize != _chunkHeader.ChunkDataTotalSize)
                    throw new ChunkCompleteException($"固定Chunk{this}首尾长度不一致,首：{ChunkHeader.ChunkDataTotalSize},尾：{currentTotalDataSize}");
            }

            var workItem = _writerWorkItem;
            var footer = new ChunkFooter(currentTotalDataSize);
            workItem.AppendData(footer.AsByteArray(), 0, ChunkFooter.Size);
            Flush();

            var oldStreamLength = workItem.WorkingStream.Length;
            var newStreamLength = ChunkHeader.Size + currentTotalDataSize + ChunkFooter.Size;

            if (newStreamLength != oldStreamLength)
                workItem.ResizeStream(newStreamLength);

            return footer;
        }
        public void Dispose()
        {
            Close();
        }
        public void Close()
        {
            lock (_writeSyncObj)
            {
                if (!_isCompleted)
                    Flush();

                if (_writerWorkItem != null)
                {
                    Helper.ExecuteActionWithoutException(() => _writerWorkItem.Dispose());
                    _writerWorkItem = null;
                }
                if (!_isMemoryChunk)
                {
                    if (_cacheItems != null)
                        _cacheItems = null;
                }
                CloseAllReaderWorkItems();
                FreeMemory();
            }
        }

        public override string ToString()
        {
            return $"({_chunkManager.Name}-#{_chunkHeader.ChunkNumber})";
        }
    }
}
