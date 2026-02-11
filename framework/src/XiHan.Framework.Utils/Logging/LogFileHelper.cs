#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LogFileHelper
// Guid:24e38f75-3d64-4a8c-b21e-cdf5b0ba1ed3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/08 12:40:03
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using XiHan.Framework.Utils.Logging.Formatters;

namespace XiHan.Framework.Utils.Logging;

/// <summary>
/// 高性能文件日志输出类（支持高并发批量写入）
/// 使用 Channels 实现高效异步日志处理，支持背压和优雅关闭
/// 适用于企业级高并发场景
/// </summary>
public static class LogFileHelper
{
    #region 常量定义

    private const int SHUTDOWN_TIMEOUT_MS = 3000;
    private const int MAX_FALLBACK_SIZE = 100 * 1024 * 1024; // 100MB
    private const int CLEANUP_CHECK_INTERVAL_MS = 3600000; // 1小时检查一次

    #endregion

    #region 私有字段

    private static readonly Lock ConfigLock = new();
    private static readonly Dictionary<string, int> LogFileCounter = [];
    private static readonly ConcurrentDictionary<string, long> FileSizeCache = new();
    private static readonly ConcurrentDictionary<string, string> CurrentLogFiles = new();
    private static readonly LogOptions Options = new();
    private static readonly LogStatistics Statistics = new();
    private static Channel<LogEntry>? _logChannel;
    private static Task? _workerTask;
    private static Task? _cleanupTask;
    private static CancellationTokenSource? _cancellationTokenSource;
    private static ILogFormatter _formatter = new TextLogFormatter();

    private static volatile bool _isShutdownRequested = false;
    private static volatile bool _isWorkerStarted = false;
    private static DateTimeOffset _lastCleanupTime = DateTimeOffset.UtcNow;

    #endregion

    #region 静态构造函数

    static LogFileHelper()
    {
        // 注册应用程序退出事件，确保优雅关闭
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
        AppDomain.CurrentDomain.DomainUnload += (_, _) => Shutdown();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Shutdown();
        };
    }

    #endregion

    #region 内部类型

    /// <summary>
    /// 配置日志选项
    /// </summary>
    /// <param name="configure">配置操作</param>
    public static void Configure(Action<LogOptions> configure)
    {
        configure?.Invoke(Options);
        ApplyConfiguration();
    }

    /// <summary>
    /// 获取当前配置信息
    /// </summary>
    /// <returns>配置选项对象</returns>
    public static LogOptions GetConfiguration()
    {
        return Options;
    }

    /// <summary>
    /// 获取日志统计信息
    /// </summary>
    /// <returns>日志统计信息</returns>
    public static LogStatistics GetStatistics()
    {
        return Statistics;
    }

    /// <summary>
    /// 重置统计信息
    /// </summary>
    public static void ResetStatistics()
    {
        Statistics.Reset();
    }

    /// <summary>
    /// 获取当前通道状态
    /// </summary>
    /// <returns>通道状态信息</returns>
    public static LogChannelStatus GetQueueStatus()
    {
        return new LogChannelStatus
        {
            ChannelCount = _logChannel?.Reader.Count ?? 0,
            IsWorkerActive = _isWorkerStarted && !_isShutdownRequested,
            IsShutdown = _isShutdownRequested,
            QueueCapacity = Options.QueueCapacity,
            BatchSize = Options.BatchSize,
            LastCleanupTime = _lastCleanupTime,
            RetentionDays = Options.RetentionDays
        };
    }

    /// <summary>
    /// 设置最小日志等级（小于该等级的日志将被忽略）
    /// </summary>
    /// <param name="level">最小日志等级，默认 Info 级别</param>
    public static void SetMinimumLevel(LogLevel level)
    {
        Options.MinimumLevel = level;
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

        Options.LogDirectory = directoryPath;
    }

    /// <summary>
    /// 设置缓冲区大小
    /// </summary>
    /// <param name="bufferSize">缓冲区大小，默认50条日志</param>
    public static void SetBufferSize(int bufferSize)
    {
        if (bufferSize <= 0)
        {
            throw new ArgumentException("缓冲区大小必须大于0", nameof(bufferSize));
        }

        Options.BatchSize = bufferSize;
    }

    /// <summary>
    /// 设置队列容量
    /// </summary>
    /// <param name="capacity">队列容量，默认10000</param>
    public static void SetQueueCapacity(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentException("队列容量必须大于0", nameof(capacity));
        }

        lock (ConfigLock)
        {
            Options.QueueCapacity = capacity;
            // 如果已启动，需要重启worker来应用新配置
            if (_isWorkerStarted && !_isShutdownRequested)
            {
                RestartWorker();
            }
        }
    }

    /// <summary>
    /// 设置批处理大小
    /// </summary>
    /// <param name="batchSize">批处理大小，默认50</param>
    public static void SetBatchSize(int batchSize)
    {
        if (batchSize <= 0)
        {
            throw new ArgumentException("批处理大小必须大于0", nameof(batchSize));
        }

        Options.BatchSize = batchSize;
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

        Options.MaxFileSize = maxSizeBytes;
    }

    /// <summary>
    /// 设置是否启用异步写入
    /// </summary>
    /// <param name="enableAsync">是否启用异步写入</param>
    public static void SetAsyncWriteEnabled(bool enableAsync)
    {
        Options.EnableAsyncWrite = enableAsync;
    }

    /// <summary>
    /// 设置是否启用统计
    /// </summary>
    /// <param name="enableStatistics">是否启用统计</param>
    public static void SetStatisticsEnabled(bool enableStatistics)
    {
        Options.EnableStatistics = enableStatistics;
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
    /// 立即刷新所有待处理日志到文件
    /// </summary>
    public static void Flush()
    {
        if (_isShutdownRequested || _logChannel == null)
        {
            return;
        }

        // 等待通道处理完当前所有日志
        var startTime = DateTimeOffset.UtcNow;
        while (_logChannel.Reader.Count > 0 && (DateTimeOffset.UtcNow - startTime).TotalMilliseconds < 5000)
        {
            Task.Delay(10).Wait();
        }
    }

    /// <summary>
    /// 优雅关闭日志系统
    /// </summary>
    public static void Shutdown()
    {
        if (_isShutdownRequested)
        {
            return;
        }

        lock (ConfigLock)
        {
            if (_isShutdownRequested)
            {
                return;
            }

            _isShutdownRequested = true;

            try
            {
                // 取消工作任务
                _cancellationTokenSource?.Cancel();

                // 完成通道写入
                if (_logChannel != null)
                {
                    _logChannel.Writer.Complete();

                    // 等待工作任务完成
                    if (_workerTask != null && !_workerTask.IsCompleted)
                    {
                        _workerTask.Wait(SHUTDOWN_TIMEOUT_MS);
                    }

                    _logChannel = null;
                }

                // 等待清理任务完成
                if (_cleanupTask != null && !_cleanupTask.IsCompleted)
                {
                    _cleanupTask.Wait(1000);
                }

                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                _workerTask = null;
                _cleanupTask = null;
                _isWorkerStarted = false;
            }
            catch
            {
                // 静默失败，由 LogHelper 负责控制台输出
            }
        }
    }

    /// <summary>
    /// 清除所有日志文件
    /// </summary>
    public static void Clear()
    {
        // 先刷新所有待处理日志
        Flush();

        lock (ConfigLock)
        {
            try
            {
                if (Directory.Exists(Options.LogDirectory))
                {
                    var logFiles = Directory.GetFiles(Options.LogDirectory, "*.log");
                    foreach (var file in logFiles)
                    {
                        File.Delete(file);
                    }

                    // 清除相关缓存
                    LogFileCounter.Clear();
                    FileSizeCache.Clear();
                }
            }
            catch (Exception ex)
            {
                // 记录清除失败的错误
                try
                {
                    var errorLogPath = Path.Combine(Options.LogDirectory, "clear_error.log");
                    var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
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

        // 先刷新所有待处理日志
        Flush();

        lock (ConfigLock)
        {
            try
            {
                if (Directory.Exists(Options.LogDirectory))
                {
                    // 匹配包含指定文件名的所有日志文件
                    var pattern = $"*{fileName}*.log";
                    var logFiles = Directory.GetFiles(Options.LogDirectory, pattern);
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

                    // 清除文件大小缓存，队列模型下不需要清除缓冲区
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
                    var errorLogPath = Path.Combine(Options.LogDirectory, "clear_error.log");
                    var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
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
    public static void Clear(DateTimeOffset date)
    {
        var dateStr = date.ToString("yyyyMMdd");

        // 先刷新所有待处理日志
        Flush();

        lock (ConfigLock)
        {
            try
            {
                if (Directory.Exists(Options.LogDirectory))
                {
                    var pattern = $"{dateStr}*.log";
                    var logFiles = Directory.GetFiles(Options.LogDirectory, pattern);
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

                    // 清除文件大小缓存，队列模型下不需要清除缓冲区
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
                    var errorLogPath = Path.Combine(Options.LogDirectory, "clear_error.log");
                    var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var errorMessage = $"[{timestamp} CLEAR_ERROR] 清除日期 '{date:yyyyMMdd}' 的日志失败: {ex.Message}{Environment.NewLine}";
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
    /// 写入文件（支持高性能队列批量写入）
    /// </summary>
    /// <param name="inputStr">日志内容</param>
    /// <param name="logLevel">日志等级</param>
    public static void WriteToFile(string? inputStr, LogLevel logLevel)
    {
        if (!IsEnabled(logLevel) || string.IsNullOrEmpty(inputStr))
        {
            return;
        }

        if (_isShutdownRequested)
        {
            // 系统关闭中，静默忽略
            return;
        }

        try
        {
            var timestamp = DateTimeOffset.UtcNow;
            var fileName = logLevel.ToString().ToLowerInvariant();
            var baseLogFileName = GetBaseLogFileName(timestamp, fileName);

            // 使用格式化器格式化日志内容
            var formattedMessage = _formatter.Format(timestamp, logLevel, inputStr ?? string.Empty, null);

            // 获取实际文件名（考虑文件大小限制）
            var logLineSize = Encoding.UTF8.GetByteCount(formattedMessage + Environment.NewLine);
            var actualLogFileName = GetOrCreateLogFileName(baseLogFileName, logLineSize);

            // 创建日志条目
            var logEntry = new LogEntry(timestamp, logLevel, inputStr ?? string.Empty, formattedMessage, actualLogFileName);

            // 确保工作任务已启动
            EnsureWorkerStarted();

            // 尝试写入通道
            if (_logChannel != null && !_logChannel.Writer.TryWrite(logEntry))
            {
                // 通道满了，根据溢出策略处理
                HandleChannelOverflow(logEntry, logLevel);
            }
            else if (Options.EnableStatistics)
            {
                // 成功写入通道，记录统计
                Statistics.RecordLogWritten(logLevel);
                Statistics.RecordFileWrite();
            }
        }
        catch (Exception ex)
        {
            // 记录写入错误
            if (Options.EnableStatistics)
            {
                Statistics.RecordWriteError();
            }

            // 如果出错，尝试直接写入错误日志
            WriteErrorLogDirectly(ex, inputStr);
        }
    }

    /// <summary>
    /// 处理通道溢出
    /// </summary>
    /// <param name="logEntry">日志条目</param>
    /// <param name="logLevel">日志级别</param>
    private static void HandleChannelOverflow(LogEntry logEntry, LogLevel logLevel)
    {
        switch (Options.OverflowPolicy)
        {
            case QueueOverflowPolicy.DropLowPriority:
                // 默认策略：低优先级丢弃，高优先级同步写入
                if (logLevel >= LogLevel.Warn)
                {
                    WriteSingleLogSync(logEntry);
                }
                else
                {
                    if (Options.EnableStatistics)
                    {
                        Statistics.RecordLogDropped();
                    }
                }
                break;

            case QueueOverflowPolicy.Block:
                // 阻塞等待（可能影响性能）
                try
                {
                    _logChannel?.Writer.WriteAsync(logEntry).AsTask().Wait(1000);
                    if (Options.EnableStatistics)
                    {
                        Statistics.RecordLogWritten(logLevel);
                        Statistics.RecordFileWrite();
                    }
                }
                catch
                {
                    WriteSingleLogSync(logEntry);
                }
                break;

            case QueueOverflowPolicy.DropOldest:
                // 丢弃最旧的（Channel 不直接支持，降级为同步写入高优先级）
                if (logLevel >= LogLevel.Warn)
                {
                    WriteSingleLogSync(logEntry);
                }
                else
                {
                    if (Options.EnableStatistics)
                    {
                        Statistics.RecordLogDropped();
                    }
                }
                break;

            case QueueOverflowPolicy.Expand:
                // 扩容（临时同步写入）
                WriteSingleLogSync(logEntry);
                break;

            default:
                // 默认丢弃
                if (Options.EnableStatistics)
                {
                    Statistics.RecordLogDropped();
                }
                break;
        }
    }

    /// <summary>
    /// 同步写入单条日志（用于紧急情况）
    /// </summary>
    /// <param name="logEntry">日志条目</param>
    private static void WriteSingleLogSync(LogEntry logEntry)
    {
        try
        {
            var filePath = Path.Combine(Options.LogDirectory, logEntry.FileName);
            var content = logEntry.FormattedMessage + Environment.NewLine;

            File.AppendAllText(filePath, content, Encoding.UTF8);

            // 更新文件大小缓存
            var contentSize = Encoding.UTF8.GetByteCount(content);
            FileSizeCache.AddOrUpdate(filePath, contentSize, (key, existing) => existing + contentSize);
        }
        catch (Exception)
        {
            WriteToFallbackFile(logEntry.FileName, [logEntry]);
        }
    }

    /// <summary>
    /// 应用配置
    /// </summary>
    private static void ApplyConfiguration()
    {
        // 应用格式化器
        _formatter = Options.LogFormat switch
        {
            LogFormat.Json => new JsonLogFormatter(),
            LogFormat.Structured => new StructuredLogFormatter(),
            _ => new TextLogFormatter()
        };
    }

    /// <summary>
    /// 是否启用日志，大于等于最小级别才输出
    /// </summary>
    /// <param name="level">日志级别</param>
    /// <returns>是否启用</returns>
    private static bool IsEnabled(LogLevel level)
    {
        return Options.MinimumLevel != LogLevel.None && level >= Options.MinimumLevel;
    }

    /// <summary>
    /// 确保工作任务已启动
    /// </summary>
    private static void EnsureWorkerStarted()
    {
        if (_isWorkerStarted || _isShutdownRequested)
        {
            return;
        }

        lock (ConfigLock)
        {
            if (_isWorkerStarted || _isShutdownRequested)
            {
                return;
            }

            try
            {
                if (!Directory.Exists(Options.LogDirectory))
                {
                    Directory.CreateDirectory(Options.LogDirectory);
                }

                // 创建有界通道（支持背压）
                var channelOptions = new BoundedChannelOptions(Options.QueueCapacity)
                {
                    FullMode = BoundedChannelFullMode.DropWrite, // 满了就丢弃（非阻塞）
                    SingleReader = true,
                    SingleWriter = false
                };
                _logChannel = Channel.CreateBounded<LogEntry>(channelOptions);

                // 创建取消令牌
                _cancellationTokenSource = new CancellationTokenSource();

                // 启动异步工作任务
                _workerTask = Task.Run(() => WorkerLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

                // 启动清理任务（如果配置了保留天数）
                if (Options.RetentionDays > 0)
                {
                    _cleanupTask = Task.Run(() => CleanupLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
                }

                _isWorkerStarted = true;
            }
            catch
            {
                // 静默失败，由 LogHelper 负责控制台输出
            }
        }
    }

    /// <summary>
    /// 重启工作任务（用于应用新配置）
    /// </summary>
    private static void RestartWorker()
    {
        if (!_isWorkerStarted)
        {
            return;
        }

        var oldShutdownState = _isShutdownRequested;
        _isShutdownRequested = true;

        try
        {
            _cancellationTokenSource?.Cancel();
            _logChannel?.Writer.Complete();
            _workerTask?.Wait(1000);
            _cleanupTask?.Wait(500);
        }
        catch { }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _workerTask = null;
        _cleanupTask = null;
        _logChannel = null;

        _isShutdownRequested = oldShutdownState;
        _isWorkerStarted = false;

        if (!_isShutdownRequested)
        {
            EnsureWorkerStarted();
        }
    }

    /// <summary>
    /// 异步清理循环（清理过期日志）
    /// </summary>
    private static async Task CleanupLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // 等待清理间隔
                    await Task.Delay(CLEANUP_CHECK_INTERVAL_MS, cancellationToken);

                    // 执行清理
                    CleanupOldLogs();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // 静默失败，由 LogHelper 负责控制台输出
                    await Task.Delay(60000, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch
        {
            // 静默失败，由 LogHelper 负责控制台输出
        }
    }

    /// <summary>
    /// 清理过期日志文件
    /// </summary>
    private static void CleanupOldLogs()
    {
        if (Options.RetentionDays <= 0 || !Directory.Exists(Options.LogDirectory))
        {
            return;
        }

        try
        {
            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-Options.RetentionDays);
            var logFiles = Directory.GetFiles(Options.LogDirectory, "*.log");

            foreach (var file in logFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTimeUtc < cutoffDate.UtcDateTime)
                    {
                        File.Delete(file);

                        // 清除相关缓存
                        FileSizeCache.TryRemove(file, out _);
                    }
                }
                catch
                {
                    // 单个文件删除失败，继续处理其他文件
                }
            }

            _lastCleanupTime = DateTimeOffset.UtcNow;
        }
        catch
        {
            // 静默失败，由 LogHelper 负责控制台输出
        }
    }

    /// <summary>
    /// 异步工作循环
    /// </summary>
    private static async Task WorkerLoopAsync(CancellationToken cancellationToken)
    {
        var batch = new List<LogEntry>(Options.BatchSize);

        try
        {
            while (!cancellationToken.IsCancellationRequested && _logChannel != null)
            {
                try
                {
                    // 异步等待并读取第一个日志条目
                    if (!await _logChannel.Reader.WaitToReadAsync(cancellationToken))
                    {
                        // 通道已完成且为空
                        break;
                    }

                    batch.Clear();

                    // 批量读取条目
                    while (batch.Count < Options.BatchSize && _logChannel.Reader.TryRead(out var entry))
                    {
                        batch.Add(entry);
                    }

                    // 批量处理
                    if (batch.Count > 0)
                    {
                        ProcessLogBatch(batch);
                    }
                }
                catch (OperationCanceledException)
                {
                    // 取消操作，正常退出
                    break;
                }
                catch (Exception)
                {
                    // 静默失败，由 LogHelper 负责控制台输出
                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                }
            }

            // 处理剩余的日志条目
            if (_logChannel != null)
            {
                while (_logChannel.Reader.TryRead(out var remainingEntry))
                {
                    batch.Clear();
                    batch.Add(remainingEntry);

                    while (batch.Count < Options.BatchSize && _logChannel.Reader.TryRead(out var entry))
                    {
                        batch.Add(entry);
                    }

                    ProcessLogBatch(batch);
                }
            }
        }
        catch
        {
            // 静默失败，由 LogHelper 负责控制台输出
        }
    }

    /// <summary>
    /// 处理日志批次
    /// </summary>
    /// <param name="batch">日志批次</param>
    private static void ProcessLogBatch(List<LogEntry> batch)
    {
        if (batch.Count == 0)
        {
            return;
        }

        // 按文件名分组
        var groups = new Dictionary<string, List<LogEntry>>();
        foreach (var entry in batch)
        {
            if (!groups.TryGetValue(entry.FileName, out var list))
            {
                list = [];
                groups[entry.FileName] = list;
            }
            list.Add(entry);
        }

        // 分组写入文件
        foreach (var group in groups)
        {
            WriteLogGroup(group.Key, group.Value);
        }
    }

    /// <summary>
    /// 写入日志组到文件
    /// </summary>
    /// <param name="fileName">文件名</param>
    /// <param name="entries">日志条目列表</param>
    private static void WriteLogGroup(string fileName, List<LogEntry> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        try
        {
            var filePath = Path.Combine(Options.LogDirectory, fileName);
            var content = string.Join(Environment.NewLine, entries.Select(e => e.FormattedMessage)) + Environment.NewLine;

            File.AppendAllText(filePath, content, Encoding.UTF8);

            // 更新文件大小缓存
            var contentSize = Encoding.UTF8.GetByteCount(content);
            FileSizeCache.AddOrUpdate(filePath, contentSize, (key, existing) => existing + contentSize);
        }
        catch (Exception)
        {
            // 写入失败，尝试备用文件
            WriteToFallbackFile(fileName, entries);
        }
    }

    /// <summary>
    /// 写入到备用文件
    /// </summary>
    /// <param name="originalFileName">原始文件名</param>
    /// <param name="entries">日志条目</param>
    private static void WriteToFallbackFile(string originalFileName, List<LogEntry> entries)
    {
        try
        {
            var fallbackFileName = $"fallback_{originalFileName}";
            var fallbackPath = Path.Combine(Options.LogDirectory, fallbackFileName);

            // 检查备用文件大小
            if (File.Exists(fallbackPath))
            {
                var fallbackInfo = new FileInfo(fallbackPath);
                if (fallbackInfo.Length > MAX_FALLBACK_SIZE)
                {
                    // 备用文件太大，静默丢弃，由 LogHelper 负责控制台输出
                    return;
                }
            }

            var content = string.Join(Environment.NewLine, entries.Select(e => e.FormattedMessage)) + Environment.NewLine;
            File.AppendAllText(fallbackPath, content, Encoding.UTF8);
        }
        catch
        {
            // 备用文件也失败，静默失败，由 LogHelper 负责控制台输出
        }
    }

    /// <summary>
    /// 根据轮转策略获取基础日志文件名
    /// </summary>
    /// <param name="timestamp">时间戳</param>
    /// <param name="levelName">日志级别名称</param>
    /// <returns>基础文件名</returns>
    private static string GetBaseLogFileName(DateTimeOffset timestamp, string levelName)
    {
        return Options.RotationPolicy switch
        {
            LogRotationPolicy.Daily => $"{timestamp:yyyyMMdd}_{levelName}.log",
            LogRotationPolicy.Hourly => $"{timestamp:yyyyMMddHH}_{levelName}.log",
            LogRotationPolicy.Size => $"{timestamp:yyyyMMdd}_{levelName}.log",
            LogRotationPolicy.Hybrid => $"{timestamp:yyyyMMdd}_{levelName}.log",
            _ => $"{timestamp:yyyyMMdd}_{levelName}.log"
        };
    }

    /// <summary>
    /// 获取或创建适合的日志文件名（考虑文件大小限制）
    /// </summary>
    /// <param name="baseFileName">基础文件名</param>
    /// <param name="expectedSize">预期写入的大小</param>
    /// <returns>实际文件名</returns>
    private static string GetOrCreateLogFileName(string baseFileName, int expectedSize)
    {
        var maxFileSize = Options.MaxFileSize;

        lock (ConfigLock)
        {
            // 检查是否已有当前活跃的文件
            if (CurrentLogFiles.TryGetValue(baseFileName, out var currentFileName))
            {
                var currentFilePath = Path.Combine(Options.LogDirectory, currentFileName);

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
        var baseFilePath = Path.Combine(Options.LogDirectory, baseFileName);
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
            newFilePath = Path.Combine(Options.LogDirectory, newFileName);

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
    /// 直接写入错误日志（不使用缓冲）
    /// </summary>
    /// <param name="ex">异常信息</param>
    /// <param name="originalMessage">原始消息</param>
    private static void WriteErrorLogDirectly(Exception ex, string? originalMessage)
    {
        try
        {
            var errorLogPath = Path.Combine(Options.LogDirectory, "error_logger.log");
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
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
            // 如果连错误日志都写不了，只能忽略
        }
    }

    /// <summary>
    /// 日志条目
    /// </summary>
    private readonly struct LogEntry
    {
        public readonly DateTimeOffset Timestamp;
        public readonly LogLevel Level;
        public readonly string Message;
        public readonly string FormattedMessage;
        public readonly string FileName;

        public LogEntry(DateTimeOffset timestamp, LogLevel level, string message, string formattedMessage, string fileName)
        {
            Timestamp = timestamp;
            Level = level;
            Message = message;
            FormattedMessage = formattedMessage;
            FileName = fileName;
        }
    }

    #endregion 私有方法
}
