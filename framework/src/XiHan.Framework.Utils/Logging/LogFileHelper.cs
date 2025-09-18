#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LogFileHelper
// Guid:24e38f75-3d64-4a8c-b21e-cdf5b0ba1ed3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-05-30 上午 12:12:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.Text;

namespace XiHan.Framework.Utils.Logging;

/// <summary>
/// 简单文件日志输出类（支持高性能批量写入）
/// </summary>
public static class LogFileHelper
{
    private static readonly Lock ObjLock = new();
    private static readonly Dictionary<string, int> LogFileCounter = [];
    private static readonly ConcurrentDictionary<string, List<string>> LogBuffers = new();
    private static readonly ConcurrentDictionary<string, long> FileSizeCache = new();
    private static readonly ConcurrentDictionary<string, string> CurrentLogFiles = new(); // 跟踪当前使用的日志文件
    private static readonly Timer FlushTimer;

    private static string _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
    private static volatile LogLevel _minimumLevel = LogLevel.Info;
    private static volatile int _bufferSize = 100; // 缓冲区大小
    private static volatile int _flushInterval = 2000; // 刷新间隔（毫秒）
    private static volatile bool _enableAsyncWrite = true; // 是否启用异步写入
    private static long _maxFileSize = 10 * 1024 * 1024; // 最大文件大小（10MB）

    static LogFileHelper()
    {
        // 初始化定时器，定期刷新缓冲区
        FlushTimer = new Timer(FlushAllBuffers, null, _flushInterval, _flushInterval);

        // 注册应用程序退出事件，确保所有日志都被写入
        AppDomain.CurrentDomain.ProcessExit += (_, _) => FlushAllBuffers(null);
        AppDomain.CurrentDomain.DomainUnload += (_, _) => FlushAllBuffers(null);
    }

    /// <summary>
    /// 设置最小日志等级（小于该等级的日志将被忽略）
    /// </summary>
    public static void SetMinimumLevel(LogLevel level)
    {
        _minimumLevel = level;
    }

    /// <summary>
    /// 设置日志文件目录
    /// </summary>
    /// <param name="directoryPath">日志文件目录路径</param>
    public static void SetLogDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }

        _logDirectory = directoryPath;
    }

    /// <summary>
    /// 设置缓冲区大小
    /// </summary>
    /// <param name="bufferSize">缓冲区大小，默认100条日志</param>
    public static void SetBufferSize(int bufferSize)
    {
        if (bufferSize <= 0)
        {
            throw new ArgumentException("缓冲区大小必须大于0", nameof(bufferSize));
        }

        _bufferSize = bufferSize;
    }

    /// <summary>
    /// 设置刷新间隔
    /// </summary>
    /// <param name="intervalMs">刷新间隔（毫秒），默认5000毫秒</param>
    public static void SetFlushInterval(int intervalMs)
    {
        if (intervalMs <= 0)
        {
            throw new ArgumentException("刷新间隔必须大于0", nameof(intervalMs));
        }

        _flushInterval = intervalMs;

        // 重新设置定时器
        FlushTimer?.Change(intervalMs, intervalMs);
    }

    /// <summary>
    /// 设置最大文件大小
    /// </summary>
    /// <param name="maxSizeBytes">最大文件大小（字节），默认10MB</param>
    public static void SetMaxFileSize(long maxSizeBytes)
    {
        if (maxSizeBytes <= 0)
        {
            throw new ArgumentException("最大文件大小必须大于0", nameof(maxSizeBytes));
        }

        Interlocked.Exchange(ref _maxFileSize, maxSizeBytes);
    }

    /// <summary>
    /// 设置是否启用异步写入
    /// </summary>
    /// <param name="enableAsync">是否启用异步写入</param>
    public static void SetAsyncWriteEnabled(bool enableAsync)
    {
        _enableAsyncWrite = enableAsync;
    }

    /// <summary>
    /// 获取当前配置信息
    /// </summary>
    /// <returns>配置信息</returns>
    public static (int BufferSize, int FlushInterval, long MaxFileSize, bool AsyncWrite) GetConfiguration()
    {
        return (_bufferSize, _flushInterval, Interlocked.Read(ref _maxFileSize), _enableAsyncWrite);
    }

    /// <summary>
    /// 正常信息
    /// </summary>
    /// <param name="inputStr">日志内容</param>
    public static void Info(string? inputStr)
    {
        WriteToFile(inputStr, LogLevel.Info);
    }

    /// <summary>
    /// 成功信息
    /// </summary>
    /// <param name="inputStr">日志内容</param>
    public static void Success(string? inputStr)
    {
        WriteToFile(inputStr, LogLevel.Success);
    }

    /// <summary>
    /// 处理、查询信息
    /// </summary>
    /// <param name="inputStr">日志内容</param>
    public static void Handle(string? inputStr)
    {
        WriteToFile(inputStr, LogLevel.Handle);
    }

    /// <summary>
    /// 警告、新增、更新信息
    /// </summary>
    /// <param name="inputStr">日志内容</param>
    public static void Warn(string? inputStr)
    {
        WriteToFile(inputStr, LogLevel.Warn);
    }

    /// <summary>
    /// 错误、删除、危险、异常信息
    /// </summary>
    /// <param name="inputStr">日志内容</param>
    public static void Error(string? inputStr)
    {
        WriteToFile(inputStr, LogLevel.Error);
    }

    /// <summary>
    /// 立即刷新所有缓冲区到文件
    /// </summary>
    public static void Flush()
    {
        FlushAllBuffers(null);
    }

    /// <summary>
    /// 清除所有日志文件
    /// </summary>
    public static void Clear()
    {
        // 先刷新所有缓冲区
        FlushAllBuffers(null);

        lock (ObjLock)
        {
            try
            {
                if (Directory.Exists(_logDirectory))
                {
                    var logFiles = Directory.GetFiles(_logDirectory, "*.log");
                    foreach (var file in logFiles)
                    {
                        File.Delete(file);
                    }

                    // 清除相关缓存
                    LogFileCounter.Clear();
                    LogBuffers.Clear();
                    FileSizeCache.Clear();
                }
            }
            catch (Exception ex)
            {
                // 记录清除失败的错误
                try
                {
                    var errorLogPath = Path.Combine(_logDirectory, "clear_error.log");
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var errorMessage = $"[{timestamp} CLEAR_ERROR] 日志清除失败: {ex.Message}{Environment.NewLine}";
                    File.AppendAllText(errorLogPath, errorMessage, Encoding.UTF8);
                }
                catch
                {
                    // 忽略备用日志记录失败
                }
            }
        }
    }

    /// <summary>
    /// 清除指定文件名的日志文件
    /// </summary>
    /// <param name="fileName">日志文件名(不含扩展名)</param>
    public static void Clear(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        // 先刷新相关缓冲区
        FlushBuffersForPattern(fileName);

        lock (ObjLock)
        {
            try
            {
                if (Directory.Exists(_logDirectory))
                {
                    // 匹配包含指定文件名的所有日志文件
                    var pattern = $"*{fileName}*.log";
                    var logFiles = Directory.GetFiles(_logDirectory, pattern);
                    foreach (var file in logFiles)
                    {
                        File.Delete(file);
                    }

                    // 清除相关缓存
                    var keysToRemove = LogFileCounter.Keys.Where(key => key.Contains(fileName)).ToList();
                    foreach (var key in keysToRemove)
                    {
                        LogFileCounter.Remove(key);
                    }

                    // 清除相关缓冲区和文件大小缓存
                    var bufferKeysToRemove = LogBuffers.Keys.Where(key => key.Contains(fileName)).ToList();
                    foreach (var key in bufferKeysToRemove)
                    {
                        LogBuffers.TryRemove(key, out _);
                    }

                    var sizeKeysToRemove = FileSizeCache.Keys.Where(key => key.Contains(fileName)).ToList();
                    foreach (var key in sizeKeysToRemove)
                    {
                        FileSizeCache.TryRemove(key, out _);
                    }
                }
            }
            catch (Exception ex)
            {
                // 记录清除失败的错误
                try
                {
                    var errorLogPath = Path.Combine(_logDirectory, "clear_error.log");
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var errorMessage = $"[{timestamp} CLEAR_ERROR] 清除文件 '{fileName}' 失败: {ex.Message}{Environment.NewLine}";
                    File.AppendAllText(errorLogPath, errorMessage, Encoding.UTF8);
                }
                catch
                {
                    // 忽略备用日志记录失败
                }
            }
        }
    }

    /// <summary>
    /// 清除指定日期的日志文件
    /// </summary>
    /// <param name="date">指定日期</param>
    public static void Clear(DateTime date)
    {
        var dateStr = date.ToString("yyyy-MM-dd");

        // 先刷新相关缓冲区
        FlushBuffersForPattern(dateStr);

        lock (ObjLock)
        {
            try
            {
                if (Directory.Exists(_logDirectory))
                {
                    var pattern = $"{dateStr}*.log";
                    var logFiles = Directory.GetFiles(_logDirectory, pattern);
                    foreach (var file in logFiles)
                    {
                        File.Delete(file);
                    }

                    // 清除相关缓存
                    var keysToRemove = LogFileCounter.Keys.Where(key => key.StartsWith(dateStr)).ToList();
                    foreach (var key in keysToRemove)
                    {
                        LogFileCounter.Remove(key);
                    }

                    // 清除相关缓冲区和文件大小缓存
                    var bufferKeysToRemove = LogBuffers.Keys.Where(key => key.StartsWith(dateStr)).ToList();
                    foreach (var key in bufferKeysToRemove)
                    {
                        LogBuffers.TryRemove(key, out _);
                    }

                    var sizeKeysToRemove = FileSizeCache.Keys.Where(key => key.StartsWith(dateStr)).ToList();
                    foreach (var key in sizeKeysToRemove)
                    {
                        FileSizeCache.TryRemove(key, out _);
                    }
                }
            }
            catch (Exception ex)
            {
                // 记录清除失败的错误
                try
                {
                    var errorLogPath = Path.Combine(_logDirectory, "clear_error.log");
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var errorMessage = $"[{timestamp} CLEAR_ERROR] 清除日期 '{date:yyyy-MM-dd}' 的日志失败: {ex.Message}{Environment.NewLine}";
                    File.AppendAllText(errorLogPath, errorMessage, Encoding.UTF8);
                }
                catch
                {
                    // 忽略备用日志记录失败
                }
            }
        }
    }

    /// <summary>
    /// 写入文件（支持高性能缓冲）
    /// </summary>
    /// <param name="inputStr">日志内容</param>
    /// <param name="logLevel">日志等级</param>
    public static void WriteToFile(string? inputStr, LogLevel logLevel)
    {
        if (!IsEnabled(logLevel) || string.IsNullOrEmpty(inputStr))
        {
            return;
        }

        try
        {
            // 确保日志目录存在
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            // 确定日志文件名
            var fileName = logLevel.ToString().ToLowerInvariant();
            var logType = logLevel.ToString().ToUpperInvariant();
            var logDate = DateTime.Now.ToString("yyyy-MM-dd");
            var baseLogFileName = $"{logDate}_{fileName}.log";

            // 格式化日志内容
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logLine = $"[{timestamp} {logType}] {inputStr}{Environment.NewLine}";
            var logLineSize = Encoding.UTF8.GetByteCount(logLine);

            // 获取当前应该写入的文件名（考虑文件大小限制）
            var actualLogFileName = GetOrCreateLogFileName(baseLogFileName, logLineSize);

            // 添加到缓冲区
            AddToBuffer(actualLogFileName, logLine, logLineSize);
        }
        catch (Exception ex)
        {
            // 如果出错，尝试直接写入错误日志
            WriteErrorLogDirectly(ex, inputStr);
        }
    }

    /// <summary>
    /// 是否启用日志
    /// </summary>
    /// <param name="level"></param>
    /// <returns></returns>
    private static bool IsEnabled(LogLevel level)
    {
        return _minimumLevel != LogLevel.None && level <= _minimumLevel;
    }

    /// <summary>
    /// 添加日志到缓冲区
    /// </summary>
    /// <param name="fileName">文件名</param>
    /// <param name="logLine">日志行</param>
    /// <param name="logLineSize">日志行字节大小</param>
    private static void AddToBuffer(string fileName, string logLine, int logLineSize)
    {
        var buffer = LogBuffers.GetOrAdd(fileName, _ => []);
        var shouldFlush = false;

        lock (buffer)
        {
            buffer.Add(logLine);
            // 预更新文件大小缓存
            var filePath = Path.Combine(_logDirectory, fileName);
            FileSizeCache.AddOrUpdate(filePath, logLineSize, (key, existingSize) => existingSize + logLineSize);
            
            if (buffer.Count >= _bufferSize)
            {
                shouldFlush = true;
            }
        }

        // 如果缓冲区满了，触发刷新
        if (shouldFlush)
        {
            if (_enableAsyncWrite)
            {
                Task.Run(() => FlushBuffer(fileName));
            }
            else
            {
                FlushBuffer(fileName);
            }
        }
    }

    /// <summary>
    /// 获取或创建适合的日志文件名（考虑文件大小限制）
    /// </summary>
    /// <param name="baseFileName">基础文件名</param>
    /// <param name="expectedSize">预期写入的大小</param>
    /// <returns>实际文件名</returns>
    private static string GetOrCreateLogFileName(string baseFileName, int expectedSize)
    {
        var maxFileSize = Interlocked.Read(ref _maxFileSize);
        
        lock (ObjLock)
        {
            // 检查是否已有当前活跃的文件
            if (CurrentLogFiles.TryGetValue(baseFileName, out var currentFileName))
            {
                var currentFilePath = Path.Combine(_logDirectory, currentFileName);
                
                // 获取当前文件大小
                var currentSize = GetFileSize(currentFilePath);
                
                // 如果当前文件加上新内容不会超过限制，继续使用
                if (currentSize + expectedSize <= maxFileSize)
                {
                    return currentFileName;
                }
            }
            
            // 需要创建新文件或这是第一次写入
            var fileName = GetNextAvailableFileName(baseFileName, expectedSize, maxFileSize);
            CurrentLogFiles[baseFileName] = fileName;
            
            return fileName;
        }
    }
    
    /// <summary>
    /// 获取下一个可用的文件名
    /// </summary>
    /// <param name="baseFileName">基础文件名</param>
    /// <param name="expectedSize">预期大小</param>
    /// <param name="maxFileSize">最大文件大小</param>
    /// <returns>可用的文件名</returns>
    private static string GetNextAvailableFileName(string baseFileName, int expectedSize, long maxFileSize)
    {
        // 先检查基础文件名
        var baseFilePath = Path.Combine(_logDirectory, baseFileName);
        var baseFileSize = GetFileSize(baseFilePath);
        
        if (baseFileSize + expectedSize <= maxFileSize)
        {
            return baseFileName;
        }
        
        // 需要创建新的编号文件
        var counter = LogFileCounter.GetValueOrDefault(baseFileName, 0);
        string newFileName;
        string newFilePath;
        
        do
        {
            counter++;
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(baseFileName);
            var extension = Path.GetExtension(baseFileName);
            newFileName = $"{fileNameWithoutExt}_{counter}{extension}";
            newFilePath = Path.Combine(_logDirectory, newFileName);
            
            var newFileSize = GetFileSize(newFilePath);
            if (newFileSize + expectedSize <= maxFileSize)
            {
                LogFileCounter[baseFileName] = counter;
                return newFileName;
            }
        } while (counter < 10000); // 防止无限循环
        
        // 如果无法找到合适的文件，返回一个新的文件名
        LogFileCounter[baseFileName] = counter;
        return newFileName;
    }
    
    /// <summary>
    /// 获取文件大小（使用缓存）
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>文件大小</returns>
    private static long GetFileSize(string filePath)
    {
        if (FileSizeCache.TryGetValue(filePath, out var cachedSize))
        {
            return cachedSize;
        }
        
        if (File.Exists(filePath))
        {
            var fileInfo = new FileInfo(filePath);
            var size = fileInfo.Length;
            FileSizeCache[filePath] = size;
            return size;
        }
        
        FileSizeCache[filePath] = 0;
        return 0;
    }

    /// <summary>
    /// 刷新指定文件的缓冲区
    /// </summary>
    /// <param name="fileName">文件名</param>
    private static void FlushBuffer(string fileName)
    {
        if (!LogBuffers.TryGetValue(fileName, out var buffer))
        {
            return;
        }

        List<string> linesToWrite;
        lock (buffer)
        {
            if (buffer.Count == 0)
            {
                return;
            }

            linesToWrite = [.. buffer];
            buffer.Clear();
        }

        try
        {
            var logFilePath = Path.Combine(_logDirectory, fileName);

            // 批量写入，减少IO操作
            var allLines = string.Join("", linesToWrite);
            File.AppendAllText(logFilePath, allLines, Encoding.UTF8);

            // 更新文件大小缓存 - 从实际文件获取准确大小
            if (File.Exists(logFilePath))
            {
                var fileInfo = new FileInfo(logFilePath);
                FileSizeCache[logFilePath] = fileInfo.Length;
            }
        }
        catch (Exception ex)
        {
            WriteErrorLogDirectly(ex, $"刷新缓冲区失败，文件: {fileName}");

            // 将失败的日志重新加入缓冲区
            lock (buffer)
            {
                buffer.InsertRange(0, linesToWrite);
            }
        }
    }

    /// <summary>
    /// 刷新所有缓冲区
    /// </summary>
    /// <param name="state">定时器状态</param>
    private static void FlushAllBuffers(object? state)
    {
        foreach (var fileName in LogBuffers.Keys.ToList())
        {
            FlushBuffer(fileName);
        }
    }

    /// <summary>
    /// 刷新匹配指定模式的缓冲区
    /// </summary>
    /// <param name="pattern">文件名模式</param>
    private static void FlushBuffersForPattern(string pattern)
    {
        var matchingFiles = LogBuffers.Keys.Where(key => key.Contains(pattern)).ToList();
        foreach (var fileName in matchingFiles)
        {
            FlushBuffer(fileName);
        }
    }

    /// <summary>
    /// 直接写入错误日志（不使用缓冲）
    /// </summary>
    /// <param name="ex">异常信息</param>
    /// <param name="originalMessage">原始消息</param>
    private static void WriteErrorLogDirectly(Exception ex, string? originalMessage)
    {
        try
        {
            var errorLogPath = Path.Combine(_logDirectory, "error_logger.log");
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var errorMessage = $"[{timestamp} LOGGER_ERROR] 日志写入失败: {ex.Message}";
            if (!string.IsNullOrEmpty(originalMessage))
            {
                errorMessage += $" | 原始消息: {originalMessage}";
            }
            errorMessage += Environment.NewLine;

            File.AppendAllText(errorLogPath, errorMessage, Encoding.UTF8);
        }
        catch
        {
            // 如果连错误日志都写不了，只能忽略了
        }
    }
}
