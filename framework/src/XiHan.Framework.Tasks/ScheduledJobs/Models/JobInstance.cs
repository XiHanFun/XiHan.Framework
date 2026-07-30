// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Tasks.ScheduledJobs.Models;

/// <summary>
/// 任务运行时实例
/// </summary>
public class JobInstance
{
    /// <summary>
    /// 实例唯一标识（唯一标识）
    /// </summary>
    public string InstanceId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 任务名称
    /// </summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// 任务信息
    /// </summary>
    public JobInfo JobInfo { get; set; } = null!;

    /// <summary>
    /// 任务状态
    /// </summary>
    public JobStatus Status { get; set; } = JobStatus.Pending;

    /// <summary>
    /// 计划执行时间
    /// </summary>
    public DateTimeOffset ScheduledAt { get; set; }

    /// <summary>
    /// 实际开始时间
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    public long? DurationMilliseconds { get; set; }

    /// <summary>
    /// 触发类型
    /// </summary>
    public JobTriggerType TriggerType { get; set; }

    /// <summary>
    /// 归属租户（为空表示 Host 任务）
    /// </summary>
    public long? TenantId { get; set; }

    /// <summary>
    /// 执行参数
    /// </summary>
    public IDictionary<string, object?>? Parameters { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 堆栈跟踪
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// 执行节点（分布式环境下使用）
    /// </summary>
    public string? ExecutionNode { get; set; }

    /// <summary>
    /// 追踪唯一标识
    /// </summary>
    public string? TraceId { get; set; }
}
