#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScriptMonitor
// Guid:49a5bdb7-1075-4aea-9dc4-f6c554e4e5ba
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/31 06:08:53
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.Text.Json;
using XiHan.Framework.Script.Core;
using XiHan.Framework.Script.Enums;
using XiHan.Framework.Script.Options;

namespace XiHan.Framework.Script.Monitoring;

/// <summary>
/// 脚本监控器
/// </summary>
public class ScriptMonitor : IDisposable
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ConcurrentQueue<ScriptExecutionLog> _executionLogs = new();
    private readonly ConcurrentDictionary<string, ScriptPerformanceInfo> _performanceCache = new();
    private readonly Timer _cleanupTimer;
    private readonly ScriptMonitorOptions _options;
    private bool _disposed;

    /// <summary>
    /// 初始化脚本监控器
    /// </summary>
    /// <param name="options">监控选项</param>
    public ScriptMonitor(ScriptMonitorOptions? options = null)
    {
        _options = options ?? ScriptMonitorOptions.Default;
        _cleanupTimer = new Timer(CleanupOldLogs, null,
            TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
    }

    /// <summary>
    /// 脚本执行事件
    /// </summary>
    public event EventHandler<ScriptExecutionEventArgs>? ScriptExecuted;

    /// <summary>
    /// 性能警告事件
    /// </summary>
    public event EventHandler<PerformanceWarningEventArgs>? PerformanceWarning;

    /// <summary>
    /// 记录脚本执行
    /// </summary>
    /// <param name="result">执行结果</param>
    /// <param name="scriptCode">脚本代码</param>
    /// <param name="scriptPath">脚本路径</param>
    public void LogExecution(ScriptResult result, string? scriptCode = null, string? scriptPath = null)
    {
        if (!_options.EnableLogging)
        {
            return;
        }

        var log = new ScriptExecutionLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.Now,
            IsSuccess = result.IsSuccess,
            ExecutionTimeMs = result.ExecutionTimeMs,
            CompilationTimeMs = result.CompilationTimeMs,
            MemoryUsageBytes = result.MemoryUsage?.MemoryIncrease ?? 0,
            ErrorMessage = result.ErrorMessage,
            ScriptCode = _options.LogScriptCode ? scriptCode : null,
            ScriptPath = scriptPath,
            FromCache = result.FromCache,
            CacheKey = result.CacheKey
        };

        _executionLogs.Enqueue(log);

        // 限制日志数量
        while (_executionLogs.Count > _options.MaxLogEntries)
        {
            _executionLogs.TryDequeue(out _);
        }

        // 更新性能信息
        UpdatePerformanceInfo(log);

        // 触发事件
        ScriptExecuted?.Invoke(this, new ScriptExecutionEventArgs(log));

        // 检查性能警告
        CheckPerformanceWarnings(log);
    }

    /// <summary>
    /// 获取执行日志
    /// </summary>
    /// <param name="count">获取数量</param>
    /// <returns>执行日志列表</returns>
    public IEnumerable<ScriptExecutionLog> GetExecutionLogs(int count = 100)
    {
        return _executionLogs.Reverse().Take(count);
    }

    /// <summary>
    /// 获取执行统计信息
    /// </summary>
    /// <returns>统计信息</returns>
    public ScriptExecutionStatistics GetStatistics()
    {
        var logs = _executionLogs.ToArray();
        var recentLogs = logs.Where(l => l.Timestamp > DateTime.Now.AddHours(-1)).ToArray();

        return new ScriptExecutionStatistics
        {
            TotalExecutions = logs.Length,
            SuccessfulExecutions = logs.Count(l => l.IsSuccess),
            FailedExecutions = logs.Count(l => !l.IsSuccess),
            AverageExecutionTimeMs = logs.Length > 0 ? logs.Average(l => l.ExecutionTimeMs) : 0,
            AverageCompilationTimeMs = logs.Where(l => l.CompilationTimeMs > 0).DefaultIfEmpty().Average(l => l?.CompilationTimeMs ?? 0),
            TotalMemoryUsageBytes = logs.Sum(l => l.MemoryUsageBytes),
            CacheHitRate = logs.Length > 0 ? (double)logs.Count(l => l.FromCache) / logs.Length * 100 : 0,
            ExecutionsLastHour = recentLogs.Length,
            AverageExecutionsPerMinute = recentLogs.Length / 60.0,
            TopErrors = GetTopErrors(logs),
            SlowScripts = GetSlowScripts(logs)
        };
    }

    /// <summary>
    /// 获取性能信息
    /// </summary>
    /// <returns>性能信息列表</returns>
    public IEnumerable<ScriptPerformanceInfo> GetPerformanceInfo()
    {
        return _performanceCache.Values.OrderByDescending(p => p.AverageExecutionTimeMs);
    }

    /// <summary>
    /// 清除所有日志
    /// </summary>
    public void ClearLogs()
    {
        while (_executionLogs.TryDequeue(out _)) { }
        _performanceCache.Clear();
    }

    /// <summary>
    /// 导出执行日志
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="format">导出格式</param>
    public async Task ExportLogsAsync(string filePath, LogExportFormat format = LogExportFormat.Json)
    {
        var logs = _executionLogs.ToArray();

        switch (format)
        {
            case LogExportFormat.Json:
                await ExportToJsonAsync(logs, filePath);
                break;

            case LogExportFormat.Csv:
                await ExportToCsvAsync(logs, filePath);
                break;

            case LogExportFormat.Xml:
                await ExportToXmlAsync(logs, filePath);
                break;

            default:
                throw new ArgumentException($"不支持的导出格式: {format}");
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _cleanupTimer?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// 获取最常见错误
    /// </summary>
    /// <param name="logs">日志列表</param>
    /// <returns>错误统计</returns>
    private static Dictionary<string, int> GetTopErrors(ScriptExecutionLog[] logs)
    {
        return logs.Where(l => !l.IsSuccess && !string.IsNullOrEmpty(l.ErrorMessage))
            .GroupBy(l => l.ErrorMessage!)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// 获取最慢脚本
    /// </summary>
    /// <param name="logs">日志列表</param>
    /// <returns>慢脚本列表</returns>
    private static IEnumerable<ScriptExecutionLog> GetSlowScripts(ScriptExecutionLog[] logs)
    {
        return logs.Where(l => l.IsSuccess)
            .OrderByDescending(l => l.ExecutionTimeMs)
            .Take(10);
    }

    /// <summary>
    /// 导出到 JSON
    /// </summary>
    private static async Task ExportToJsonAsync(ScriptExecutionLog[] logs, string filePath)
    {
        var json = JsonSerializer.Serialize(logs, JsonSerializerOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// 导出到 CSV
    /// </summary>
    private static async Task ExportToCsvAsync(ScriptExecutionLog[] logs, string filePath)
    {
        using var writer = new StreamWriter(filePath);

        // 写入标题行
        await writer.WriteLineAsync("Id,Timestamp,IsSuccess,ExecutionTimeMs,CompilationTimeMs,MemoryUsageBytes,ErrorMessage,ScriptPath,FromCache,CacheKey");

        // 写入数据行
        foreach (var log in logs)
        {
            await writer.WriteLineAsync($"{log.Id},{log.Timestamp:yyyy-MM-dd HH:mm:ss},{log.IsSuccess},{log.ExecutionTimeMs},{log.CompilationTimeMs},{log.MemoryUsageBytes},\"{log.ErrorMessage}\",\"{log.ScriptPath}\",{log.FromCache},\"{log.CacheKey}\"");
        }
    }

    /// <summary>
    /// 导出到 XML
    /// </summary>
    private static async Task ExportToXmlAsync(ScriptExecutionLog[] logs, string filePath)
    {
        using var writer = new StreamWriter(filePath);
        await writer.WriteLineAsync("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        await writer.WriteLineAsync("<ExecutionLogs>");

        foreach (var log in logs)
        {
            await writer.WriteLineAsync("  <Log>");
            await writer.WriteLineAsync($"    <Id>{log.Id}</Id>");
            await writer.WriteLineAsync($"    <Timestamp>{log.Timestamp:yyyy-MM-dd HH:mm:ss}</Timestamp>");
            await writer.WriteLineAsync($"    <IsSuccess>{log.IsSuccess}</IsSuccess>");
            await writer.WriteLineAsync($"    <ExecutionTimeMs>{log.ExecutionTimeMs}</ExecutionTimeMs>");
            await writer.WriteLineAsync($"    <CompilationTimeMs>{log.CompilationTimeMs}</CompilationTimeMs>");
            await writer.WriteLineAsync($"    <MemoryUsageBytes>{log.MemoryUsageBytes}</MemoryUsageBytes>");
            await writer.WriteLineAsync($"    <ErrorMessage><![CDATA[{log.ErrorMessage}]]></ErrorMessage>");
            await writer.WriteLineAsync($"    <ScriptPath><![CDATA[{log.ScriptPath}]]></ScriptPath>");
            await writer.WriteLineAsync($"    <FromCache>{log.FromCache}</FromCache>");
            await writer.WriteLineAsync($"    <CacheKey><![CDATA[{log.CacheKey}]]></CacheKey>");
            await writer.WriteLineAsync("  </Log>");
        }

        await writer.WriteLineAsync("</ExecutionLogs>");
    }

    /// <summary>
    /// 更新性能信息
    /// </summary>
    /// <param name="log">执行日志</param>
    private void UpdatePerformanceInfo(ScriptExecutionLog log)
    {
        if (string.IsNullOrEmpty(log.CacheKey))
        {
            return;
        }

        _performanceCache.AddOrUpdate(log.CacheKey,
            _ => new ScriptPerformanceInfo
            {
                CacheKey = log.CacheKey,
                ExecutionCount = 1,
                TotalExecutionTimeMs = log.ExecutionTimeMs,
                AverageExecutionTimeMs = log.ExecutionTimeMs,
                MinExecutionTimeMs = log.ExecutionTimeMs,
                MaxExecutionTimeMs = log.ExecutionTimeMs,
                LastExecutionTime = log.Timestamp,
                SuccessCount = log.IsSuccess ? 1 : 0,
                FailureCount = log.IsSuccess ? 0 : 1
            },
            (_, existing) =>
            {
                existing.ExecutionCount++;
                existing.TotalExecutionTimeMs += log.ExecutionTimeMs;
                existing.AverageExecutionTimeMs = existing.TotalExecutionTimeMs / existing.ExecutionCount;
                existing.MinExecutionTimeMs = Math.Min(existing.MinExecutionTimeMs, log.ExecutionTimeMs);
                existing.MaxExecutionTimeMs = Math.Max(existing.MaxExecutionTimeMs, log.ExecutionTimeMs);
                existing.LastExecutionTime = log.Timestamp;

                if (log.IsSuccess)
                {
                    existing.SuccessCount++;
                }
                else
                {
                    existing.FailureCount++;
                }

                return existing;
            });
    }

    /// <summary>
    /// 检查性能警告
    /// </summary>
    /// <param name="log">执行日志</param>
    private void CheckPerformanceWarnings(ScriptExecutionLog log)
    {
        var warnings = new List<string>();

        if (log.ExecutionTimeMs > _options.SlowExecutionThresholdMs)
        {
            warnings.Add($"脚本执行时间过长: {log.ExecutionTimeMs}ms (阈值: {_options.SlowExecutionThresholdMs}ms)");
        }

        if (log.MemoryUsageBytes > _options.HighMemoryUsageThresholdBytes)
        {
            warnings.Add($"内存使用量过高: {log.MemoryUsageBytes} bytes (阈值: {_options.HighMemoryUsageThresholdBytes} bytes)");
        }

        if (!log.IsSuccess && !string.IsNullOrEmpty(log.ErrorMessage))
        {
            warnings.Add($"脚本执行失败: {log.ErrorMessage}");
        }

        if (warnings.Count > 0)
        {
            PerformanceWarning?.Invoke(this, new PerformanceWarningEventArgs(log, warnings));
        }
    }

    /// <summary>
    /// 清理旧日志
    /// </summary>
    /// <param name="state">状态</param>
    private void CleanupOldLogs(object? state)
    {
        if (!_options.EnableLogCleanup)
        {
            return;
        }

        var cutoffTime = DateTime.Now.AddHours(-_options.LogRetentionHours);
        var tempList = new List<ScriptExecutionLog>();

        while (_executionLogs.TryDequeue(out var log))
        {
            if (log.Timestamp > cutoffTime)
            {
                tempList.Add(log);
            }
        }

        foreach (var log in tempList)
        {
            _executionLogs.Enqueue(log);
        }
    }
}
