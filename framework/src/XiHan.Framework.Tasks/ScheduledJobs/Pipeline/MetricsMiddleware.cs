#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MetricsMiddleware
// Guid:c85dbf9b-436c-4048-b512-a521fa9e9a7c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 17:00:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using System.Diagnostics;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Pipeline;

/// <summary>
/// 度量统计中间件
/// </summary>
public class MetricsMiddleware : IJobMiddleware
{
    private readonly ILogger<MetricsMiddleware> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public MetricsMiddleware(ILogger<MetricsMiddleware> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 执行中间件逻辑
    /// </summary>
    public async Task<JobResult> InvokeAsync(IJobContext context, JobExecutionDelegate next)
    {
        var startTime = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await next(context);

            stopwatch.Stop();
            var duration = stopwatch.ElapsedMilliseconds;

            // 记录度量信息
            RecordMetrics(context, result, duration);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var duration = stopwatch.ElapsedMilliseconds;

            var result = JobResult.Failure(ex.Message, ex, TimeSpan.FromMilliseconds(duration));
            RecordMetrics(context, result, duration);

            throw;
        }
    }

    /// <summary>
    /// 记录度量信息
    /// </summary>
    private void RecordMetrics(IJobContext context, JobResult result, long durationMs)
    {
        var jobName = context.JobInstance.JobName;

        _logger.LogDebug(
            "任务度量 - 名称: {JobName}, 状态: {Status}, 耗时: {Duration}ms, 重试次数: {RetryCount}",
            jobName, result.Status, durationMs, result.RetryCount);

        // 在这里可以集成 Prometheus、OpenTelemetry 等监控系统
        // 例如：
        // _metricsCollector.RecordJobDuration(jobName, durationMs);
        // _metricsCollector.RecordJobStatus(jobName, result.Status);
    }
}
