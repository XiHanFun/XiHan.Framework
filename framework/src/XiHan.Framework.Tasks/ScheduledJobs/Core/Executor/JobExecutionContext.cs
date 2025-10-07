#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JobExecutionContext.cs
// Guid:28347e45-fdd1-4f4a-b014-4d4a996af9c8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 14:24:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Core.Executor;

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
        StartedAt = DateTimeOffset.UtcNow;
        AttemptCount = 1;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// 任务实例信息
    /// </summary>
    public JobInstance JobInstance { get; }

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
