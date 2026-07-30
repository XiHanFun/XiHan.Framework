// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Tasks.BackgroundServices;

/// <summary>
/// 服务状态信息
/// </summary>
public class XiHanBackgroundServiceStatusInfo
{
    /// <summary>
    /// 服务名称
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用任务处理
    /// </summary>
    public bool IsTaskProcessingEnabled { get; set; }

    /// <summary>
    /// 最大并发任务数
    /// </summary>
    public int MaxConcurrentTasks { get; set; }

    /// <summary>
    /// 当前运行任务数
    /// </summary>
    public int CurrentRunningTasks { get; set; }

    /// <summary>
    /// 空闲延迟时间
    /// </summary>
    public int IdleDelayMilliseconds { get; set; }

    /// <summary>
    /// 是否启用重试
    /// </summary>
    public bool RetryEnabled { get; set; }

    /// <summary>
    /// 统计信息
    /// </summary>
    public StatisticsSummary Statistics { get; set; } = new();
}
