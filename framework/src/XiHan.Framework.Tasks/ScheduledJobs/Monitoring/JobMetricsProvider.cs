#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JobMetricsProvider
// Guid:8b44fc66-a394-41ac-9b3c-b7803601fdbc
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 17:18:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Monitoring;

/// <summary>
/// 任务度量提供者
/// </summary>
public class JobMetricsProvider
{
    private readonly ConcurrentDictionary<string, JobMetrics> _metrics = new();

    /// <summary>
    /// 记录任务执行
    /// </summary>
    public void RecordExecution(string jobName, JobStatus status, long durationMs)
    {
        var metrics = _metrics.GetOrAdd(jobName, _ => new JobMetrics { JobName = jobName });

        lock (metrics)
        {
            metrics.TotalExecutions++;
            metrics.LastExecutionTime = DateTimeOffset.UtcNow;
            metrics.LastDurationMs = durationMs;

            if (metrics.MinDurationMs == 0 || durationMs < metrics.MinDurationMs)
            {
                metrics.MinDurationMs = durationMs;
            }

            if (durationMs > metrics.MaxDurationMs)
            {
                metrics.MaxDurationMs = durationMs;
            }

            // 计算平均耗时
            metrics.AverageDurationMs = ((metrics.AverageDurationMs * (metrics.TotalExecutions - 1)) + durationMs) / metrics.TotalExecutions;

            switch (status)
            {
                case JobStatus.Succeeded:
                    metrics.SuccessCount++;
                    break;

                case JobStatus.Failed:
                    metrics.FailureCount++;
                    break;

                case JobStatus.Canceled:
                    metrics.CancelledCount++;
                    break;
            }
        }
    }

    /// <summary>
    /// 获取任务度量信息
    /// </summary>
    public JobMetrics? GetMetrics(string jobName)
    {
        return _metrics.TryGetValue(jobName, out var metrics) ? metrics : null;
    }

    /// <summary>
    /// 获取所有任务度量信息
    /// </summary>
    public IReadOnlyDictionary<string, JobMetrics> GetAllMetrics()
    {
        return _metrics;
    }

    /// <summary>
    /// 清空度量信息
    /// </summary>
    public void Clear(string? jobName = null)
    {
        if (jobName != null)
        {
            _metrics.TryRemove(jobName, out _);
        }
        else
        {
            _metrics.Clear();
        }
    }
}

/// <summary>
/// 任务度量信息
/// </summary>
public class JobMetrics
{
    /// <summary>
    /// 任务名称
    /// </summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// 总执行次数
    /// </summary>
    public long TotalExecutions { get; set; }

    /// <summary>
    /// 成功次数
    /// </summary>
    public long SuccessCount { get; set; }

    /// <summary>
    /// 失败次数
    /// </summary>
    public long FailureCount { get; set; }

    /// <summary>
    /// 取消次数
    /// </summary>
    public long CancelledCount { get; set; }

    /// <summary>
    /// 最后执行时间
    /// </summary>
    public DateTimeOffset? LastExecutionTime { get; set; }

    /// <summary>
    /// 最后执行耗时（毫秒）
    /// </summary>
    public long LastDurationMs { get; set; }

    /// <summary>
    /// 平均执行耗时（毫秒）
    /// </summary>
    public long AverageDurationMs { get; set; }

    /// <summary>
    /// 最小执行耗时（毫秒）
    /// </summary>
    public long MinDurationMs { get; set; }

    /// <summary>
    /// 最大执行耗时（毫秒）
    /// </summary>
    public long MaxDurationMs { get; set; }

    /// <summary>
    /// 成功率
    /// </summary>
    public double SuccessRate => TotalExecutions > 0 ? (double)SuccessCount / TotalExecutions * 100 : 0;

    /// <summary>
    /// 失败率
    /// </summary>
    public double FailureRate => TotalExecutions > 0 ? (double)FailureCount / TotalExecutions * 100 : 0;
}
