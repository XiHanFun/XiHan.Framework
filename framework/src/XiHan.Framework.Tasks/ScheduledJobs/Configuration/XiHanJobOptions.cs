// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Tasks.ScheduledJobs.Configuration;

/// <summary>
/// 曦寒任务调度配置选项
/// </summary>
public class XiHanJobOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Tasks:ScheduledJobs";

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
