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
    private static readonly Timer FlushTimer;

    private static string _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
    private static volatile LogLevel _minimumLevel = LogLevel.Info;
    private static volatile int _bufferSize = 100; // 缓冲区大小
    private static volatile int _flushInterval = 5000; // 刷新间隔（毫秒）
    private static long _maxFileSize = 10 * 1024 * 1024; // 最大文件大小（10MB）
    private static volatile bool _enableAsyncWrite = true; // 是否启用异步写入

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

            // 检查文件大小并获取实际文件名
            var actualLogFileName = GetActualLogFileName(baseLogFileName);

            // 格式化日志内容
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logLine = $"[{timestamp} {logType}] {inputStr}{Environment.NewLine}";

            // 添加到缓冲区
            AddToBuffer(actualLogFileName, logLine);
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
    private static void AddToBuffer(string fileName, string logLine)
    {
        var buffer = LogBuffers.GetOrAdd(fileName, _ => new List<string>());
        bool shouldFlush = false;

        lock (buffer)
        {
            buffer.Add(logLine);
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
    /// 获取实际的日志文件名（考虑文件大小限制）
    /// </summary>
    /// <param name="baseFileName">基础文件名</param>
    /// <returns>实际文件名</returns>
    private static string GetActualLogFileName(string baseFileName)
    {
        var logFilePath = Path.Combine(_logDirectory, baseFileName);

        // 使用缓存的文件大小信息，减少IO操作
        if (!FileSizeCache.TryGetValue(logFilePath, out var cachedSize))
        {
            if (File.Exists(logFilePath))
            {
                var fileInfo = new FileInfo(logFilePath);
                cachedSize = fileInfo.Length;
                FileSizeCache[logFilePath] = cachedSize;
            }
            else
            {
                cachedSize = 0;
                FileSizeCache[logFilePath] = cachedSize;
            }
        }

        // 如果文件大小超过限制，创建新文件
        var maxFileSize = Interlocked.Read(ref _maxFileSize);
        if (cachedSize > maxFileSize)
        {
            lock (ObjLock)
            {
                // 双重检查，避免并发问题
                if (FileSizeCache.TryGetValue(logFilePath, out var size) && size > Interlocked.Read(ref _maxFileSize))
                {
                    // 获取或初始化计数器
                    if (!LogFileCounter.TryGetValue(baseFileName, out var counter))
                    {
                        counter = 0;
                    }

                    counter++;
                    LogFileCounter[baseFileName] = counter;

                    // 创建新的日志文件名
                    var fileNameWithoutExt = Path.GetFileNameWithoutExtension(baseFileName);
                    var extension = Path.GetExtension(baseFileName);
                    var newFileName = $"{fileNameWithoutExt}_{counter}{extension}";

                    var newFilePath = Path.Combine(_logDirectory, newFileName);
                    FileSizeCache[newFilePath] = 0; // 新文件初始大小为0

                    return newFileName;
                }
            }
        }

        return baseFileName;
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

            linesToWrite = new List<string>(buffer);
            buffer.Clear();
        }

        try
        {
            var logFilePath = Path.Combine(_logDirectory, fileName);

            // 批量写入，减少IO操作
            var allLines = string.Join("", linesToWrite);
            File.AppendAllText(logFilePath, allLines, Encoding.UTF8);

            // 更新文件大小缓存
            if (FileSizeCache.TryGetValue(logFilePath, out var currentSize))
            {
                var newSize = currentSize + Encoding.UTF8.GetByteCount(allLines);
                FileSizeCache[logFilePath] = newSize;
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
