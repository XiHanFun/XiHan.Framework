#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TimeoutMiddleware
// Guid:d7cbf532-b684-46f4-9d20-15327eb09146
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 14:34:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Pipeline;

/// <summary>
/// 超时控制中间件
/// </summary>
public class TimeoutMiddleware : IJobMiddleware
{
    private readonly ILogger<TimeoutMiddleware> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public TimeoutMiddleware(ILogger<TimeoutMiddleware> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 执行中间件逻辑
    /// </summary>
    public async Task<JobResult> InvokeAsync(IJobContext context, JobExecutionDelegate next)
    {
        var timeoutMs = context.JobInstance.JobInfo.TimeoutMilliseconds;

        if (timeoutMs <= 0)
        {
            return await next(context);
        }

        using var timeoutCts = new CancellationTokenSource(timeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            context.CancellationToken,
            timeoutCts.Token);

        try
        {
            // 创建新的上下文，使用组合的取消令牌
            var timeoutContext = new TimeoutJobContext(context, linkedCts.Token);
            return await next(timeoutContext);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _logger.LogWarning(
                "任务 {JobName} 执行超时（超时时间: {Timeout}ms）",
                context.JobInstance.JobName, timeoutMs);

            return JobResult.Failure($"任务执行超时（超时时间: {timeoutMs}ms）");
        }
    }

    /// <summary>
    /// 带超时的任务上下文包装器
    /// </summary>
    private class TimeoutJobContext : IJobContext
    {
        private readonly IJobContext _innerContext;
        private readonly CancellationToken _cancellationToken;

        public TimeoutJobContext(IJobContext innerContext, CancellationToken cancellationToken)
        {
            _innerContext = innerContext;
            _cancellationToken = cancellationToken;
        }

        public JobInstance JobInstance => _innerContext.JobInstance;
        public long? TenantId => _innerContext.TenantId;
        public IDictionary<string, object?> Parameters => _innerContext.Parameters;
        public IServiceProvider ServiceProvider => _innerContext.ServiceProvider;
        public string TraceId => _innerContext.TraceId;
        public DateTimeOffset StartedAt => _innerContext.StartedAt;

        public int AttemptCount
        {
            get => _innerContext.AttemptCount;
            set => _innerContext.AttemptCount = value;
        }

        public CancellationToken CancellationToken => _cancellationToken;
    }
}
