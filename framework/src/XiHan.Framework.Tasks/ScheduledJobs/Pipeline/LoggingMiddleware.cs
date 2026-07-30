// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Pipeline;

/// <summary>
/// 日志中间件
/// </summary>
public class LoggingMiddleware : IJobMiddleware
{
    private readonly ILogger<LoggingMiddleware> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public LoggingMiddleware(ILogger<LoggingMiddleware> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 执行中间件逻辑
    /// </summary>
    public async Task<JobResult> InvokeAsync(IJobContext context, JobExecutionDelegate next)
    {
        var jobName = context.JobInstance.JobName;
        var instanceId = context.JobInstance.InstanceId;
        var traceId = context.TraceId;

        _logger.LogInformation("开始执行任务: {JobName} (实例唯一标识: {InstanceId}, 追踪唯一标识: {TraceId})",
            jobName, instanceId, traceId);

        var startTime = DateTimeOffset.UtcNow;

        try
        {
            var result = await next(context);

            var duration = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;

            if (result.IsSuccess)
            {
                _logger.LogInformation("任务执行成功: {JobName} (实例唯一标识: {InstanceId}, 耗时: {Duration}ms)",
                    jobName, instanceId, duration);
            }
            else
            {
                _logger.LogWarning("任务执行失败: {JobName} (实例唯一标识: {InstanceId}, 耗时: {Duration}ms, 错误: {ErrorMessage})",
                    jobName, instanceId, duration, result.ErrorMessage);
            }

            return result;
        }
        catch (Exception ex)
        {
            var duration = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            _logger.LogError(ex,
                "任务执行异常: {JobName} (实例唯一标识: {InstanceId}, 耗时: {Duration}ms)",
                jobName, instanceId, duration);
            throw;
        }
    }
}
