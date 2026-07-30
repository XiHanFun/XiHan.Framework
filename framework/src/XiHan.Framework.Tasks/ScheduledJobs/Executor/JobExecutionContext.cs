// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Executor;

/// <summary>
/// 任务执行上下文实现
/// </summary>
public class JobExecutionContext : IJobContext
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public JobExecutionContext(
        JobInstance jobInstance,
        IDictionary<string, object?>? parameters,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        JobInstance = jobInstance ?? throw new ArgumentNullException(nameof(jobInstance));
        Parameters = parameters ?? new Dictionary<string, object?>();
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        TraceId = jobInstance.TraceId ?? Guid.NewGuid().ToString("N");
        TenantId = jobInstance.TenantId;
        StartedAt = DateTimeOffset.UtcNow;
        AttemptCount = 1;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// 任务实例信息
    /// </summary>
    public JobInstance JobInstance { get; }

    /// <summary>
    /// 当前任务租户（为空表示 Host 任务）
    /// </summary>
    public long? TenantId { get; }

    /// <summary>
    /// 任务参数
    /// </summary>
    public IDictionary<string, object?> Parameters { get; }

    /// <summary>
    /// 服务提供者
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 追踪标识
    /// </summary>
    public string TraceId { get; }

    /// <summary>
    /// 任务执行开始时间
    /// </summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>
    /// 尝试次数
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// 取消令牌
    /// </summary>
    public CancellationToken CancellationToken { get; }
}
