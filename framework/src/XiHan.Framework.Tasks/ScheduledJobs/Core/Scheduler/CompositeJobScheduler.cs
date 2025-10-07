#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CompositeJobScheduler.cs
// Guid:74ea6dfb-4244-4c30-b623-44962dd6dd0f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 15:24:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Core.Scheduler;

/// <summary>
/// 复合任务调度器（支持多种触发方式）
/// </summary>
public class CompositeJobScheduler : IJobScheduler
{
    private readonly JobRegistry _jobRegistry;
    private readonly JobTriggerManager _triggerManager;
    private readonly IJobExecutor _jobExecutor;
    private readonly ILogger<CompositeJobScheduler> _logger;
    private readonly IJobStore _jobStore;
    private readonly IServiceProvider _serviceProvider;

    private readonly Lock _lock = new();
    private Timer? _schedulerTimer;
    private bool _isRunning;

    /// <summary>
    /// 构造函数
    /// </summary>
    public CompositeJobScheduler(
        IJobExecutor jobExecutor,
        ILogger<CompositeJobScheduler> logger,
        IJobStore jobStore,
        IServiceProvider serviceProvider)
    {
        _jobRegistry = new JobRegistry();
        _triggerManager = new JobTriggerManager();
        _jobExecutor = jobExecutor ?? throw new ArgumentNullException(nameof(jobExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jobStore = jobStore ?? throw new ArgumentNullException(nameof(jobStore));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// 注册任务
    /// </summary>
    public void RegisterJob(JobInfo jobInfo)
    {
        ArgumentNullException.ThrowIfNull(jobInfo);

        _logger.LogInformation("注册任务: {JobName}, 类型: {TriggerType}", jobInfo.JobName, jobInfo.TriggerType);
        _jobRegistry.Register(jobInfo);

        // 计算下次触发时间
        UpdateNextFireTime(jobInfo);
    }

    /// <summary>
    /// 取消注册任务
    /// </summary>
    public void UnregisterJob(string jobName)
    {
        _logger.LogInformation("取消注册任务: {JobName}", jobName);
        _jobRegistry.Unregister(jobName);
        _triggerManager.RemoveTriggerState(jobName);
    }

    /// <summary>
    /// 暂停任务
    /// </summary>
    public void PauseJob(string jobName)
    {
        _logger.LogInformation("暂停任务: {JobName}", jobName);
        _triggerManager.PauseJob(jobName);
    }

    /// <summary>
    /// 恢复任务
    /// </summary>
    public void ResumeJob(string jobName)
    {
        _logger.LogInformation("恢复任务: {JobName}", jobName);
        _triggerManager.ResumeJob(jobName);
    }

    /// <summary>
    /// 手动触发任务
    /// </summary>
    public async Task<string> TriggerJobAsync(string jobName, IDictionary<string, object?>? parameters = null)
    {
        var jobInfo = _jobRegistry.GetJob(jobName) ?? throw new InvalidOperationException($"任务不存在: {jobName}");
        _logger.LogInformation("手动触发任务: {JobName}", jobName);
        return await ExecuteJobAsync(jobInfo, JobTriggerType.Manual, parameters);
    }

    /// <summary>
    /// 获取下次执行时间
    /// </summary>
    public DateTimeOffset? GetNextFireTime(string jobName)
    {
        var state = _triggerManager.GetTriggerState(jobName);
        return state?.NextFireTime;
    }

    /// <summary>
    /// 获取所有已注册的任务信息
    /// </summary>
    public IReadOnlyList<JobInfo> GetAllJobs()
    {
        return _jobRegistry.GetAllJobs();
    }

    /// <summary>
    /// 启动调度器
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_isRunning)
            {
                return Task.CompletedTask;
            }

            _logger.LogInformation("启动任务调度器");
            _isRunning = true;

            // 每秒检查一次
            _schedulerTimer = new Timer(
                callback: _ => CheckAndFireJobs(),
                state: null,
                dueTime: TimeSpan.FromSeconds(1),
                period: TimeSpan.FromSeconds(1));

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 停止调度器
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_isRunning)
            {
                return Task.CompletedTask;
            }

            _logger.LogInformation("停止任务调度器");
            _isRunning = false;

            _schedulerTimer?.Dispose();
            _schedulerTimer = null;

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 判断是否应该触发
    /// </summary>
    private static bool ShouldFire(JobInfo jobInfo, JobTriggerState? state)
    {
        var nextFireTime = state?.NextFireTime;
        if (!nextFireTime.HasValue)
        {
            return false;
        }

        return nextFireTime.Value <= DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// 检查并触发任务
    /// </summary>
    private void CheckAndFireJobs()
    {
        var jobs = _jobRegistry.GetAllJobs();
        var now = DateTimeOffset.UtcNow;

        foreach (var jobInfo in jobs)
        {
            if (!jobInfo.IsEnabled)
            {
                continue;
            }

            var state = _triggerManager.GetTriggerState(jobInfo.JobName);
            if (state?.IsPaused == true)
            {
                continue;
            }

            // 检查是否需要触发
            if (ShouldFire(jobInfo, state))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ExecuteJobAsync(jobInfo, jobInfo.TriggerType, jobInfo.DefaultParameters);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "执行任务失败: {JobName}", jobInfo.JobName);
                    }
                });
            }
        }
    }

    /// <summary>
    /// 执行任务
    /// </summary>
    private async Task<string> ExecuteJobAsync(
        JobInfo jobInfo,
        JobTriggerType triggerType,
        IDictionary<string, object?>? parameters = null)
    {
        // 检查并发控制
        if (!jobInfo.AllowConcurrent)
        {
            var runningInstances = await _jobStore.GetRunningInstancesAsync(jobInfo.JobName);
            if (runningInstances.Any())
            {
                _logger.LogWarning("任务 {JobName} 不允许并发执行，跳过本次触发", jobInfo.JobName);
                return string.Empty;
            }
        }

        // 创建任务实例
        var instance = new JobInstance
        {
            JobName = jobInfo.JobName,
            JobInfo = jobInfo,
            TriggerType = triggerType,
            ScheduledAt = DateTimeOffset.UtcNow,
            Parameters = parameters,
            TraceId = Guid.NewGuid().ToString("N"),
            ExecutionNode = Environment.MachineName
        };

        // 记录触发
        _triggerManager.RecordTrigger(jobInfo.JobName, instance.ScheduledAt);

        // 更新下次触发时间
        UpdateNextFireTime(jobInfo);

        // 执行任务
        _ = Task.Run(async () =>
        {
            try
            {
                await _jobExecutor.ExecuteAsync(instance, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "任务执行异常: {JobName} ({InstanceId})", jobInfo.JobName, instance.InstanceId);
            }
        });

        return instance.InstanceId;
    }

    /// <summary>
    /// 更新下次触发时间
    /// </summary>
    private void UpdateNextFireTime(JobInfo jobInfo)
    {
        var nextFireTime = jobInfo.TriggerType switch
        {
            JobTriggerType.Cron when !string.IsNullOrWhiteSpace(jobInfo.CronExpression)
                => CronScheduler.GetNextFireTime(jobInfo.CronExpression),
            JobTriggerType.Interval when jobInfo.Interval.HasValue
                => IntervalScheduler.GetNextFireTime(jobInfo.Interval.Value),
            JobTriggerType.Delay when jobInfo.Delay.HasValue
                => DateTimeOffset.UtcNow.Add(jobInfo.Delay.Value),
            _ => null
        };

        _triggerManager.UpdateNextFireTime(jobInfo.JobName, nextFireTime);
    }
}
