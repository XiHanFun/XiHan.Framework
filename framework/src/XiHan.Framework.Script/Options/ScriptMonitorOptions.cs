#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScriptMonitorOptions
// Guid:e7295a70-d8b8-403b-bf43-a3ab7422415d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/01 11:07:29
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Script.Options;

/// <summary>
/// 脚本监控选项
/// </summary>
public class ScriptMonitorOptions
{
    /// <summary>
    /// 默认监控选项
    /// </summary>
    public static ScriptMonitorOptions Default => new();

    /// <summary>
    /// 是否启用日志记录
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// 是否记录脚本代码
    /// </summary>
    public bool LogScriptCode { get; set; }

    /// <summary>
    /// 最大日志条目数
    /// </summary>
    public int MaxLogEntries { get; set; } = 10000;

    /// <summary>
    /// 慢执行阈值(毫秒)
    /// </summary>
    public long SlowExecutionThresholdMs { get; set; } = 5000;

    /// <summary>
    /// 高内存使用阈值(字节)
    /// </summary>
    public long HighMemoryUsageThresholdBytes { get; set; } = 100 * 1024 * 1024; // 100MB

    /// <summary>
    /// 是否启用日志清理
    /// </summary>
    public bool EnableLogCleanup { get; set; } = true;

    /// <summary>
    /// 日志保留时间(小时)
    /// </summary>
    public int LogRetentionHours { get; set; } = 24;

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
