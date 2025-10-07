#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanJobOptions
// Guid:218f1eaf-2e25-41d7-a28f-e15a7c8aaa7a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 15:02:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Tasks.ScheduledJobs.Configuration;

/// <summary>
/// 曦寒任务调度配置选项
/// </summary>
public class XiHanJobOptions
{
    /// <summary>
    /// 是否启用任务调度
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 是否自动发现并注册任务
    /// </summary>
    public bool AutoDiscoverJobs { get; set; } = true;

    /// <summary>
    /// 任务扫描程序集名称模式
    /// </summary>
    public string[] JobAssemblyPatterns { get; set; } = ["*.Jobs", "*.Tasks"];

    /// <summary>
    /// 默认任务超时时间（毫秒）
    /// </summary>
    public int DefaultTimeoutMilliseconds { get; set; } = 300000; // 5分钟

    /// <summary>
    /// 是否启用分布式锁
    /// </summary>
    public bool EnableDistributedLock { get; set; } = false;

    /// <summary>
    /// 历史记录保留天数
    /// </summary>
    public int HistoryRetentionDays { get; set; } = 30;

    /// <summary>
    /// 是否启用性能监控
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// 任务执行节点名称
    /// </summary>
    public string? NodeName { get; set; }
}
