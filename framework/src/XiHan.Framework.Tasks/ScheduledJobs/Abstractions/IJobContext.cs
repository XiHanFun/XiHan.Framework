// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Abstractions;

/// <summary>
/// 任务执行上下文接口
/// </summary>
public interface IJobContext
{
    /// <summary>
    /// 任务实例信息
    /// </summary>
    JobInstance JobInstance { get; }

    /// <summary>
    /// 当前任务租户（为空表示 Host 任务）
    /// </summary>
    long? TenantId { get; }

    /// <summary>
    /// 任务参数
    /// </summary>
    IDictionary<string, object?> Parameters { get; }

    /// <summary>
    /// 服务提供者（用于依赖注入）
    /// </summary>
    IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 追踪标识
    /// </summary>
    string TraceId { get; }

    /// <summary>
    /// 任务执行开始时间
    /// </summary>
    DateTimeOffset StartedAt { get; }

    /// <summary>
    /// 尝试次数（含重试）
    /// </summary>
    int AttemptCount { get; set; }

    /// <summary>
    /// 取消令牌
    /// </summary>
    CancellationToken CancellationToken { get; }
}
