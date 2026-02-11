#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LogStatistics
// Guid:8c7d6e5f-4d3c-2b1a-0f9e-8d7c6b5a4c3d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Logging;

/// <summary>
/// 日志统计信息
/// 使用原子操作确保线程安全
/// </summary>
public class LogStatistics
{
    private long _totalLogsWritten;
    private long _consoleLogsWritten;
    private long _fileLogsWritten;
    private long _logsDropped;
    private long _writeErrors;
    private long _infoCount;
    private long _successCount;
    private long _handleCount;
    private long _warnCount;
    private long _errorCount;

    /// <summary>
    /// 总写入日志数
    /// </summary>
    public long TotalLogsWritten => Interlocked.Read(ref _totalLogsWritten);

    /// <summary>
    /// 控制台写入数
    /// </summary>
    public long ConsoleLogsWritten => Interlocked.Read(ref _consoleLogsWritten);

    /// <summary>
    /// 文件写入数
    /// </summary>
    public long FileLogsWritten => Interlocked.Read(ref _fileLogsWritten);

    /// <summary>
    /// 丢弃日志数
    /// </summary>
    public long LogsDropped => Interlocked.Read(ref _logsDropped);

    /// <summary>
    /// 写入错误数
    /// </summary>
    public long WriteErrors => Interlocked.Read(ref _writeErrors);

    /// <summary>
    /// Info 级别日志数
    /// </summary>
    public long InfoCount => Interlocked.Read(ref _infoCount);

    /// <summary>
    /// Success 级别日志数
    /// </summary>
    public long SuccessCount => Interlocked.Read(ref _successCount);

    /// <summary>
    /// Handle 级别日志数
    /// </summary>
    public long HandleCount => Interlocked.Read(ref _handleCount);

    /// <summary>
    /// Warn 级别日志数
    /// </summary>
    public long WarnCount => Interlocked.Read(ref _warnCount);

    /// <summary>
    /// Error 级别日志数
    /// </summary>
    public long ErrorCount => Interlocked.Read(ref _errorCount);

    /// <summary>
    /// 重置统计信息
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _totalLogsWritten, 0);
        Interlocked.Exchange(ref _consoleLogsWritten, 0);
        Interlocked.Exchange(ref _fileLogsWritten, 0);
        Interlocked.Exchange(ref _logsDropped, 0);
        Interlocked.Exchange(ref _writeErrors, 0);
        Interlocked.Exchange(ref _infoCount, 0);
        Interlocked.Exchange(ref _successCount, 0);
        Interlocked.Exchange(ref _handleCount, 0);
        Interlocked.Exchange(ref _warnCount, 0);
        Interlocked.Exchange(ref _errorCount, 0);
    }

    /// <summary>
    /// 获取统计信息摘要
    /// </summary>
    public string GetSummary()
    {
        return $"总日志: {TotalLogsWritten}, 控制台: {ConsoleLogsWritten}, " +
               $"文件: {FileLogsWritten}, 丢弃: {LogsDropped}, 错误: {WriteErrors}\n" +
               $"级别统计 - Info: {InfoCount}, Success: {SuccessCount}, " +
               $"Handle: {HandleCount}, Warn: {WarnCount}, Error: {ErrorCount}";
    }

    /// <summary>
    /// 获取按级别统计的字典
    /// </summary>
    public Dictionary<LogLevel, long> GetLevelCounts()
    {
        return new Dictionary<LogLevel, long>
        {
            [LogLevel.Info] = InfoCount,
            [LogLevel.Success] = SuccessCount,
            [LogLevel.Handle] = HandleCount,
            [LogLevel.Warn] = WarnCount,
            [LogLevel.Error] = ErrorCount
        };
    }

    /// <summary>
    /// 记录日志写入
    /// </summary>
    /// <param name="level">日志级别</param>
    internal void RecordLogWritten(LogLevel level)
    {
        Interlocked.Increment(ref _totalLogsWritten);

        switch (level)
        {
            case LogLevel.Info:
                Interlocked.Increment(ref _infoCount);
                break;

            case LogLevel.Success:
                Interlocked.Increment(ref _successCount);
                break;

            case LogLevel.Handle:
                Interlocked.Increment(ref _handleCount);
                break;

            case LogLevel.Warn:
                Interlocked.Increment(ref _warnCount);
                break;

            case LogLevel.Error:
                Interlocked.Increment(ref _errorCount);
                break;
        }
    }

    /// <summary>
    /// 记录控制台写入
    /// </summary>
    internal void RecordConsoleWrite()
    {
        Interlocked.Increment(ref _consoleLogsWritten);
    }

    /// <summary>
    /// 记录文件写入
    /// </summary>
    internal void RecordFileWrite()
    {
        Interlocked.Increment(ref _fileLogsWritten);
    }

    /// <summary>
    /// 记录日志丢弃
    /// </summary>
    internal void RecordLogDropped()
    {
        Interlocked.Increment(ref _logsDropped);
    }

    /// <summary>
    /// 记录写入错误
    /// </summary>
    internal void RecordWriteError()
    {
        Interlocked.Increment(ref _writeErrors);
    }
}
