#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RetryMiddleware
// Guid:3764d541-105a-4ba3-b237-2d712d06f11b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 15:34:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Pipeline;

/// <summary>
/// 重试中间件
/// </summary>
public class RetryMiddleware : IJobMiddleware
{
    private readonly ILogger<RetryMiddleware> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public RetryMiddleware(ILogger<RetryMiddleware> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 执行中间件逻辑
    /// </summary>
    public async Task<JobResult> InvokeAsync(IJobContext context, JobExecutionDelegate next)
    {
        var retryPolicy = context.JobInstance.JobInfo.RetryPolicy ?? JobRetryPolicy.None;
        var maxRetryCount = retryPolicy.MaxRetryCount;

        JobResult? lastResult = null;
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxRetryCount + 1; attempt++)
        {
            context.AttemptCount = attempt;

            try
            {
                var result = await next(context);

                if (result.IsSuccess)
                {
                    if (attempt > 1)
                    {
                        _logger.LogInformation(
                            "任务 {JobName} 在第 {Attempt} 次尝试后成功",
                            context.JobInstance.JobName, attempt);
                    }
                    return result;
                }

                lastResult = result;

                // 如果还有重试机会
                if (attempt <= maxRetryCount)
                {
                    var delay = retryPolicy.CalculateDelay(attempt);
                    _logger.LogWarning(
                        "任务 {JobName} 第 {Attempt} 次执行失败，{Delay}ms 后重试",
                        context.JobInstance.JobName, attempt, delay.TotalMilliseconds);

                    await Task.Delay(delay, context.CancellationToken);
                }
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("任务 {JobName} 被取消", context.JobInstance.JobName);
                return JobResult.Canceled();
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt <= maxRetryCount)
                {
                    var delay = retryPolicy.CalculateDelay(attempt);
                    _logger.LogWarning(ex,
                        "任务 {JobName} 第 {Attempt} 次执行异常，{Delay}ms 后重试",
                        context.JobInstance.JobName, attempt, delay.TotalMilliseconds);

                    await Task.Delay(delay, context.CancellationToken);
                }
                else
                {
                    _logger.LogError(ex,
                        "任务 {JobName} 执行失败，已达到最大重试次数 {MaxRetryCount}",
                        context.JobInstance.JobName, maxRetryCount);
                }
            }
        }

        // 所有重试都失败了
        if (lastException != null)
        {
            return JobResult.Failure(
                $"任务执行失败（已重试 {maxRetryCount} 次）: {lastException.Message}",
                lastException);
        }

        return lastResult ?? JobResult.Failure($"任务执行失败（已重试 {maxRetryCount} 次）");
    }
}
