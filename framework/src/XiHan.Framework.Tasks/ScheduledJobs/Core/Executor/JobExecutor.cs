#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JobExecutor.cs
// Guid:8c1e0f1f-501d-4030-a705-5f6a6031fadc
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 17:36:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Models;
using XiHan.Framework.Utils.Serialization.Json;

namespace XiHan.Framework.Tasks.ScheduledJobs.Core.Executor;

/// <summary>
/// 任务执行器实现
/// </summary>
public class JobExecutor : IJobExecutor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobExecutor> _logger;
    private readonly IJobStore _jobStore;
    private readonly IEnumerable<IJobMiddleware> _middlewares;

    /// <summary>
    /// 构造函数
    /// </summary>
    public JobExecutor(
        IServiceProvider serviceProvider,
        ILogger<JobExecutor> logger,
        IJobStore jobStore,
        IEnumerable<IJobMiddleware> middlewares)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jobStore = jobStore ?? throw new ArgumentNullException(nameof(jobStore));
        _middlewares = middlewares ?? throw new ArgumentNullException(nameof(middlewares));
    }

    /// <summary>
    /// 执行任务
    /// </summary>
    public async Task<JobResult> ExecuteAsync(
        JobInstance jobInstance,
        IDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        jobInstance.StartedAt = startTime;
        jobInstance.Status = JobStatus.Running;

        try
        {
            // 保存任务实例状态
            await _jobStore.SaveJobInstanceAsync(jobInstance);

            // 创建作用域
            using var scope = _serviceProvider.CreateScope();
            var scopedServiceProvider = scope.ServiceProvider;

            // 创建执行上下文
            var context = new JobExecutionContext(
                jobInstance,
                parameters,
                scopedServiceProvider,
                cancellationToken);

            // 创建任务实例
            var job = ActivatorUtilities.CreateInstance(scopedServiceProvider, jobInstance.JobInfo.JobType) as IJob
                ?? throw new InvalidOperationException($"无法创建任务实例: {jobInstance.JobInfo.JobType.Name}");

            // 构建并执行管道
            var pipeline = new JobExecutionPipeline(scopedServiceProvider);
            foreach (var middleware in _middlewares)
            {
                pipeline.Use(middleware);
            }

            var result = await pipeline.ExecuteAsync(context, job);

            // 更新执行结果
            var endTime = DateTimeOffset.UtcNow;
            jobInstance.CompletedAt = endTime;
            jobInstance.DurationMilliseconds = (long)(endTime - startTime).TotalMilliseconds;
            jobInstance.Status = result.Status;
            jobInstance.RetryCount = context.AttemptCount - 1;

            if (!result.IsSuccess)
            {
                jobInstance.ErrorMessage = result.ErrorMessage;
                jobInstance.StackTrace = result.Exception?.StackTrace;
            }

            // 更新任务状态
            await _jobStore.UpdateJobStatusAsync(jobInstance.InstanceId, result.Status);

            // 保存执行历史
            await SaveHistoryAsync(jobInstance, result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "任务执行失败: {JobName} ({InstanceId})", jobInstance.JobName, jobInstance.InstanceId);

            var endTime = DateTimeOffset.UtcNow;
            jobInstance.CompletedAt = endTime;
            jobInstance.DurationMilliseconds = (long)(endTime - startTime).TotalMilliseconds;
            jobInstance.Status = JobStatus.Failed;
            jobInstance.ErrorMessage = ex.Message;
            jobInstance.StackTrace = ex.StackTrace;

            await _jobStore.UpdateJobStatusAsync(jobInstance.InstanceId, JobStatus.Failed);

            var result = JobResult.Failure(ex.Message, ex, endTime - startTime);
            await SaveHistoryAsync(jobInstance, result);

            return result;
        }
    }

    /// <summary>
    /// 保存执行历史
    /// </summary>
    private async Task SaveHistoryAsync(JobInstance jobInstance, JobResult result)
    {
        var history = new JobHistory
        {
            InstanceId = jobInstance.InstanceId,
            JobName = jobInstance.JobName,
            Status = result.Status,
            StartedAt = jobInstance.StartedAt ?? DateTimeOffset.UtcNow,
            CompletedAt = jobInstance.CompletedAt,
            DurationMilliseconds = jobInstance.DurationMilliseconds,
            TriggerType = jobInstance.TriggerType,
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            StackTrace = jobInstance.StackTrace,
            RetryCount = jobInstance.RetryCount,
            ExecutionNode = jobInstance.ExecutionNode,
            TraceId = jobInstance.TraceId,
            ParametersJson = jobInstance.Parameters != null
                ? JsonHelper.Serialize(jobInstance.Parameters)
                : null
        };

        try
        {
            await _jobStore.SaveJobHistoryAsync(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存任务执行历史失败: {JobName} ({InstanceId})", jobInstance.JobName, jobInstance.InstanceId);
        }
    }
}
