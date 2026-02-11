#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LockMiddleware
// Guid:1e38ccbe-0590-4fa2-8d18-b30a72af242c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 16:55:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Pipeline;

/// <summary>
/// 分布式锁中间件
/// </summary>
public class LockMiddleware : IJobMiddleware
{
    private readonly IJobLockProvider? _lockProvider;
    private readonly ILogger<LockMiddleware> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public LockMiddleware(ILogger<LockMiddleware> logger, IJobLockProvider? lockProvider = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _lockProvider = lockProvider;
    }

    /// <summary>
    /// 执行中间件逻辑
    /// </summary>
    public async Task<JobResult> InvokeAsync(IJobContext context, JobExecutionDelegate next)
    {
        // 如果允许并发或没有锁提供者，直接执行
        if (context.JobInstance.JobInfo.AllowConcurrent || _lockProvider == null)
        {
            return await next(context);
        }

        var lockKey = $"job:lock:{context.JobInstance.JobName}";
        var lockExpiry = TimeSpan.FromMilliseconds(context.JobInstance.JobInfo.TimeoutMilliseconds + 5000);

        ILockToken? lockToken = null;

        try
        {
            // 尝试获取锁
            lockToken = await _lockProvider.TryAcquireLockAsync(lockKey, lockExpiry, context.CancellationToken);

            if (lockToken == null)
            {
                _logger.LogWarning("无法获取任务锁，任务 {JobName} 可能正在其他节点执行",
                    context.JobInstance.JobName);

                return JobResult.Failure("无法获取任务锁，任务可能正在其他节点执行");
            }

            _logger.LogDebug("成功获取任务锁: {JobName}", context.JobInstance.JobName);

            // 执行任务
            return await next(context);
        }
        finally
        {
            // 释放锁
            if (lockToken != null)
            {
                try
                {
                    await lockToken.DisposeAsync();
                    _logger.LogDebug("成功释放任务锁: {JobName}", context.JobInstance.JobName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "释放任务锁失败: {JobName}", context.JobInstance.JobName);
                }
            }
        }
    }
}
