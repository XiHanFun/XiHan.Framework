#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MonitoringModels
// Guid:h4i5j6k7-l8m9-n0o1-p2q3-r4s5t6u7v8w9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/2 11:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Script.Monitoring;

/// <summary>
/// 脚本执行日志
/// </summary>
public class ScriptExecutionLog
{
    /// <summary>
    /// 日志ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 执行时间戳
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// 是否执行成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 执行时间（毫秒）
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// 编译时间（毫秒）
    /// </summary>
    public long CompilationTimeMs { get; set; }

    /// <summary>
    /// 内存使用量（字节）
    /// </summary>
    public long MemoryUsageBytes { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 脚本代码
    /// </summary>
    public string? ScriptCode { get; set; }

    /// <summary>
    /// 脚本路径
    /// </summary>
    public string? ScriptPath { get; set; }

    /// <summary>
    /// 是否来自缓存
    /// </summary>
    public bool FromCache { get; set; }

    /// <summary>
    /// 缓存键
    /// </summary>
    public string? CacheKey { get; set; }
}

/// <summary>
/// 脚本性能信息
/// </summary>
public class ScriptPerformanceInfo
{
    /// <summary>
    /// 缓存键
    /// </summary>
    public string CacheKey { get; set; } = string.Empty;

    /// <summary>
    /// 执行次数
    /// </summary>
    public long ExecutionCount { get; set; }

    /// <summary>
    /// 总执行时间（毫秒）
    /// </summary>
    public long TotalExecutionTimeMs { get; set; }

    /// <summary>
    /// 平均执行时间（毫秒）
    /// </summary>
    public double AverageExecutionTimeMs { get; set; }

    /// <summary>
    /// 最小执行时间（毫秒）
    /// </summary>
    public long MinExecutionTimeMs { get; set; }

    /// <summary>
    /// 最大执行时间（毫秒）
    /// </summary>
    public long MaxExecutionTimeMs { get; set; }

    /// <summary>
    /// 最后执行时间
    /// </summary>
    public DateTime LastExecutionTime { get; set; }

    /// <summary>
    /// 成功次数
    /// </summary>
    public long SuccessCount { get; set; }

    /// <summary>
    /// 失败次数
    /// </summary>
    public long FailureCount { get; set; }

    /// <summary>
    /// 成功率
    /// </summary>
    public double SuccessRate => ExecutionCount > 0 ? (double)SuccessCount / ExecutionCount * 100 : 0;
}

/// <summary>
/// 脚本执行统计信息
/// </summary>
public class ScriptExecutionStatistics
{
    /// <summary>
    /// 总执行次数
    /// </summary>
    public int TotalExecutions { get; set; }

    /// <summary>
    /// 成功执行次数
    /// </summary>
    public int SuccessfulExecutions { get; set; }

    /// <summary>
    /// 失败执行次数
    /// </summary>
    public int FailedExecutions { get; set; }

    /// <summary>
    /// 平均执行时间（毫秒）
    /// </summary>
    public double AverageExecutionTimeMs { get; set; }

    /// <summary>
    /// 平均编译时间（毫秒）
    /// </summary>
    public double AverageCompilationTimeMs { get; set; }

    /// <summary>
    /// 总内存使用量（字节）
    /// </summary>
    public long TotalMemoryUsageBytes { get; set; }

    /// <summary>
    /// 缓存命中率（百分比）
    /// </summary>
    public double CacheHitRate { get; set; }

    /// <summary>
    /// 最近一小时执行次数
    /// </summary>
    public int ExecutionsLastHour { get; set; }

    /// <summary>
    /// 平均每分钟执行次数
    /// </summary>
    public double AverageExecutionsPerMinute { get; set; }

    /// <summary>
    /// 最常见错误
    /// </summary>
    public Dictionary<string, int> TopErrors { get; set; } = [];

    /// <summary>
    /// 最慢脚本
    /// </summary>
    public IEnumerable<ScriptExecutionLog> SlowScripts { get; set; } = [];

    /// <summary>
    /// 成功率（百分比）
    /// </summary>
    public double SuccessRate => TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions * 100 : 0;
}

/// <summary>
/// 脚本监控选项
/// </summary>
public class ScriptMonitorOptions
{
    /// <summary>
    /// 是否启用日志记录
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// 是否记录脚本代码
    /// </summary>
    public bool LogScriptCode { get; set; } = false;

    /// <summary>
    /// 最大日志条目数
    /// </summary>
    public int MaxLogEntries { get; set; } = 10000;

    /// <summary>
    /// 慢执行阈值（毫秒）
    /// </summary>
    public long SlowExecutionThresholdMs { get; set; } = 5000;

    /// <summary>
    /// 高内存使用阈值（字节）
    /// </summary>
    public long HighMemoryUsageThresholdBytes { get; set; } = 100 * 1024 * 1024; // 100MB

    /// <summary>
    /// 是否启用日志清理
    /// </summary>
    public bool EnableLogCleanup { get; set; } = true;

    /// <summary>
    /// 日志保留时间（小时）
    /// </summary>
    public int LogRetentionHours { get; set; } = 24;

    /// <summary>
    /// 默认监控选项
    /// </summary>
    public static ScriptMonitorOptions Default => new();

    /// <summary>
    /// 高性能监控选项
    /// </summary>
    public static ScriptMonitorOptions HighPerformance()
    {
        return new ScriptMonitorOptions
        {
            EnableLogging = true,
            LogScriptCode = false,
            MaxLogEntries = 5000,
            SlowExecutionThresholdMs = 1000,
            HighMemoryUsageThresholdBytes = 50 * 1024 * 1024, // 50MB
            EnableLogCleanup = true,
            LogRetentionHours = 12
        };
    }

    /// <summary>
    /// 详细监控选项
    /// </summary>
    public static ScriptMonitorOptions Verbose()
    {
        return new ScriptMonitorOptions
        {
            EnableLogging = true,
            LogScriptCode = true,
            MaxLogEntries = 50000,
            SlowExecutionThresholdMs = 10000,
            HighMemoryUsageThresholdBytes = 500 * 1024 * 1024, // 500MB
            EnableLogCleanup = true,
            LogRetentionHours = 72
        };
    }
}

/// <summary>
/// 脚本执行事件参数
/// </summary>
public class ScriptExecutionEventArgs : EventArgs
{
    /// <summary>
    /// 执行日志
    /// </summary>
    public ScriptExecutionLog Log { get; }

    /// <summary>
    /// 初始化脚本执行事件参数
    /// </summary>
    /// <param name="log">执行日志</param>
    public ScriptExecutionEventArgs(ScriptExecutionLog log)
    {
        Log = log;
    }
}

/// <summary>
/// 性能警告事件参数
/// </summary>
public class PerformanceWarningEventArgs : EventArgs
{
    /// <summary>
    /// 执行日志
    /// </summary>
    public ScriptExecutionLog Log { get; }

    /// <summary>
    /// 警告信息列表
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// 初始化性能警告事件参数
    /// </summary>
    /// <param name="log">执行日志</param>
    /// <param name="warnings">警告信息列表</param>
    public PerformanceWarningEventArgs(ScriptExecutionLog log, IReadOnlyList<string> warnings)
    {
        Log = log;
        Warnings = warnings;
    }
}

/// <summary>
/// 日志导出格式
/// </summary>
public enum LogExportFormat
{
    /// <summary>
    /// JSON 格式
    /// </summary>
    Json,

    /// <summary>
    /// CSV 格式
    /// </summary>
    Csv,

    /// <summary>
    /// XML 格式
    /// </summary>
    Xml
} 
