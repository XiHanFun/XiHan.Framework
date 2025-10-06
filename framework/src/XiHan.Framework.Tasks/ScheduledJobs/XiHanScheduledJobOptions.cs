#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanScheduledJobOptions
// Guid:2c3d4e5f-6a7b-8c9d-0e1f-2a3b4c5d6e7f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/6 22:31:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Tasks.ScheduledJobs;

/// <summary>
/// 调度任务配置选项
/// </summary>
public class XiHanScheduledJobOptions
{
    /// <summary>
    /// 是否启用调度任务
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 调度器名称
    /// </summary>
    public string SchedulerName { get; set; } = "XiHanScheduler";

    /// <summary>
    /// 线程池大小
    /// </summary>
    public int ThreadPoolSize { get; set; } = 10;

    /// <summary>
    /// 是否允许任务并发执行
    /// </summary>
    public bool AllowConcurrentExecution { get; set; } = false;

    /// <summary>
    /// 任务失败时是否自动重试
    /// </summary>
    public bool AutoRetryOnFailure { get; set; } = false;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// 重试间隔时间（秒）
    /// </summary>
    public int RetryIntervalSeconds { get; set; } = 60;
}
