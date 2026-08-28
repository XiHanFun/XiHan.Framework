// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Scheduler;

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

    /// <summary>
    /// 已经抢到触发权、正在走"记录触发 + 重排下次时间 + 派发执行"这一段的任务名
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> _firingJobs = new();

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

        // 截止时间：超过后不再触发
        if (jobInfo.EndTime.HasValue && DateTimeOffset.UtcNow > jobInfo.EndTime.Value)
        {
            return false;
        }

        // 重复次数上限：达到后不再触发（-1 不限）
        if (jobInfo.RepeatCount >= 0 && state!.TriggerCount >= jobInfo.RepeatCount)
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
            if (!ShouldFire(jobInfo, state))
            {
                continue;
            }

            // 触发权抢占：ShouldFire 的判定在定时器线程，而"记录触发 + 重排下次触发时间"原来发生在
            // Task.Run 的异步体内部（ExecuteJobAsync 里）。定时器每秒一跳，上一跳派出去的执行体只要还没
            // 跑到重排（例如 AllowConcurrent=false 时要先 await 存储查运行中实例，线程池繁忙时也会延迟），
            // 下一跳就会读到没被推进的 NextFireTime，把同一次排期重复触发。
            // 这里先在定时器线程内独占触发权，执行体整体结束（含重排）后再释放，把那段窗口封死。
            // 注意释放而不是"清掉 NextFireTime"：ExecuteJobAsync 因并发控制跳过本次时不会重排，
            // 保留原有的 NextFireTime 才能维持"下一跳继续重试"的既有行为。
            if (!_firingJobs.TryAdd(jobInfo.JobName, 0))
            {
                continue;
            }

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
                finally
                {
                    _firingJobs.TryRemove(jobInfo.JobName, out _);
                }
            });
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
            TenantId = ResolveTenantId(jobInfo, parameters),
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
            // Delay 为一次性延迟：仅在从未触发过时排期，触发过后不再续排（否则会按 Delay 周期无限重复）
            JobTriggerType.Delay when jobInfo.Delay.HasValue
                => _triggerManager.GetTriggerState(jobInfo.JobName)?.LastFireTime is null
                    ? DateTimeOffset.UtcNow.Add(jobInfo.Delay.Value)
                    : null,
            _ => null
        };

        // 截止时间之后的排期一律截断
        if (nextFireTime.HasValue && jobInfo.EndTime.HasValue && nextFireTime.Value > jobInfo.EndTime.Value)
        {
            nextFireTime = null;
        }

        // 可自动调度的触发类型算不出下次时间时显性告警（Cron 解析失败/表达式无解等
        // 此前被静默吞掉，任务表现为"注册了但永不执行"，无从排障）
        if (!nextFireTime.HasValue
            && jobInfo.TriggerType is JobTriggerType.Cron or JobTriggerType.Interval
            && jobInfo.IsEnabled)
        {
            _logger.LogWarning(
                "任务 {JobName}（{TriggerType}）无法计算下次触发时间，将不会被自动调度；请检查表达式/间隔/截止时间配置（Cron: {Cron}）",
                jobInfo.JobName, jobInfo.TriggerType, jobInfo.CronExpression);
        }

        _triggerManager.UpdateNextFireTime(jobInfo.JobName, nextFireTime);
    }

    /// <summary>
    /// 解析任务所属租户
    /// </summary>
    /// <param name="jobInfo"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    private static long? ResolveTenantId(JobInfo jobInfo, IDictionary<string, object?>? parameters)
    {
        if (parameters is not null
            && parameters.TryGetValue("tenantId", out var tenantIdValue)
            && tenantIdValue is not null
            && long.TryParse(tenantIdValue.ToString(), out var parameterTenantId))
        {
            return parameterTenantId;
        }

        if (jobInfo.TenantId.HasValue)
        {
            return jobInfo.TenantId.Value;
        }

        var currentTenant = AsyncLocalCurrentTenantAccessor.Instance.Current;
        if (!string.IsNullOrWhiteSpace(currentTenant?.Name)
            && long.TryParse(currentTenant.Name, out var scopedTenantId))
        {
            return scopedTenantId;
        }

        return null;
    }
}
