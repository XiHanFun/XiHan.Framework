#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanScheduledJobManager
// Guid:5f6a7b8c-9d0e-1f2a-3b4c-5d6e7f8a9b0c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/6 22:34:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using System.Collections.Specialized;
using XiHan.Framework.Tasks.Crons;

namespace XiHan.Framework.Tasks.ScheduledJobs;

/// <summary>
/// 调度任务管理器实现
/// </summary>
public class XiHanScheduledJobManager : IScheduledJobManager
{
    private readonly ILogger<XiHanScheduledJobManager> _logger;
    private readonly XiHanScheduledJobOptions _options;
    private IScheduler? _scheduler;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">配置选项</param>
    public XiHanScheduledJobManager(
        ILogger<XiHanScheduledJobManager> logger,
        IOptions<XiHanScheduledJobOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 添加调度任务
    /// </summary>
    public async Task<bool> AddJobAsync<TJob>(
        string jobName,
        string jobGroup,
        string cronExpression,
        string? description = null,
        bool startImmediately = false,
        int priority = 5) where TJob : IJob
    {
        try
        {
            // 验证 Cron 表达式
            if (!CronHelper.IsValidExpression(cronExpression))
            {
                _logger.LogError("无效的 Cron 表达式: {CronExpression}", cronExpression);
                return false;
            }

            var scheduler = await GetSchedulerAsync();

            // 创建任务
            var jobDetail = JobBuilder.Create<TJob>()
                .WithIdentity(jobName, jobGroup)
                .WithDescription(description)
                .Build();

            // 创建触发器
            var trigger = TriggerBuilder.Create()
                .WithIdentity($"{jobName}_trigger", jobGroup)
                .WithCronSchedule(cronExpression)
                .WithPriority(priority)
                .Build();

            // 调度任务
            await scheduler.ScheduleJob(jobDetail, trigger);

            _logger.LogInformation("成功添加调度任务: {JobGroup}.{JobName}, Cron: {CronExpression}",
                jobGroup, jobName, cronExpression);

            // 如果需要立即执行
            if (startImmediately)
            {
                await TriggerJobAsync(jobName, jobGroup);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加调度任务失败: {JobGroup}.{JobName}", jobGroup, jobName);
            return false;
        }
    }

    /// <summary>
    /// 删除调度任务
    /// </summary>
    public async Task<bool> RemoveJobAsync(string jobName, string jobGroup)
    {
        try
        {
            var scheduler = await GetSchedulerAsync();
            var jobKey = new JobKey(jobName, jobGroup);

            if (!await scheduler.CheckExists(jobKey))
            {
                _logger.LogWarning("任务不存在: {JobGroup}.{JobName}", jobGroup, jobName);
                return false;
            }

            var result = await scheduler.DeleteJob(jobKey);

            if (result)
            {
                _logger.LogInformation("成功删除调度任务: {JobGroup}.{JobName}", jobGroup, jobName);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除调度任务失败: {JobGroup}.{JobName}", jobGroup, jobName);
            return false;
        }
    }

    /// <summary>
    /// 暂停调度任务
    /// </summary>
    public async Task<bool> PauseJobAsync(string jobName, string jobGroup)
    {
        try
        {
            var scheduler = await GetSchedulerAsync();
            var jobKey = new JobKey(jobName, jobGroup);

            if (!await scheduler.CheckExists(jobKey))
            {
                _logger.LogWarning("任务不存在: {JobGroup}.{JobName}", jobGroup, jobName);
                return false;
            }

            await scheduler.PauseJob(jobKey);
            _logger.LogInformation("成功暂停调度任务: {JobGroup}.{JobName}", jobGroup, jobName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "暂停调度任务失败: {JobGroup}.{JobName}", jobGroup, jobName);
            return false;
        }
    }

    /// <summary>
    /// 恢复调度任务
    /// </summary>
    public async Task<bool> ResumeJobAsync(string jobName, string jobGroup)
    {
        try
        {
            var scheduler = await GetSchedulerAsync();
            var jobKey = new JobKey(jobName, jobGroup);

            if (!await scheduler.CheckExists(jobKey))
            {
                _logger.LogWarning("任务不存在: {JobGroup}.{JobName}", jobGroup, jobName);
                return false;
            }

            await scheduler.ResumeJob(jobKey);
            _logger.LogInformation("成功恢复调度任务: {JobGroup}.{JobName}", jobGroup, jobName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复调度任务失败: {JobGroup}.{JobName}", jobGroup, jobName);
            return false;
        }
    }

    /// <summary>
    /// 立即执行调度任务
    /// </summary>
    public async Task<bool> TriggerJobAsync(string jobName, string jobGroup)
    {
        try
        {
            var scheduler = await GetSchedulerAsync();
            var jobKey = new JobKey(jobName, jobGroup);

            if (!await scheduler.CheckExists(jobKey))
            {
                _logger.LogWarning("任务不存在: {JobGroup}.{JobName}", jobGroup, jobName);
                return false;
            }

            await scheduler.TriggerJob(jobKey);
            _logger.LogInformation("成功触发调度任务: {JobGroup}.{JobName}", jobGroup, jobName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "触发调度任务失败: {JobGroup}.{JobName}", jobGroup, jobName);
            return false;
        }
    }

    /// <summary>
    /// 更新任务的 Cron 表达式
    /// </summary>
    public async Task<bool> UpdateJobCronAsync(string jobName, string jobGroup, string cronExpression)
    {
        try
        {
            // 验证 Cron 表达式
            if (!CronHelper.IsValidExpression(cronExpression))
            {
                _logger.LogError("无效的 Cron 表达式: {CronExpression}", cronExpression);
                return false;
            }

            var scheduler = await GetSchedulerAsync();
            var triggerKey = new TriggerKey($"{jobName}_trigger", jobGroup);

            var trigger = await scheduler.GetTrigger(triggerKey);
            if (trigger == null)
            {
                _logger.LogWarning("触发器不存在: {JobGroup}.{JobName}", jobGroup, jobName);
                return false;
            }

            // 创建新的触发器
            var newTrigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .WithCronSchedule(cronExpression)
                .Build();

            // 重新调度
            await scheduler.RescheduleJob(triggerKey, newTrigger);

            _logger.LogInformation("成功更新调度任务 Cron 表达式: {JobGroup}.{JobName}, 新表达式: {CronExpression}",
                jobGroup, jobName, cronExpression);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新调度任务 Cron 表达式失败: {JobGroup}.{JobName}", jobGroup, jobName);
            return false;
        }
    }

    /// <summary>
    /// 获取所有调度任务信息
    /// </summary>
    public async Task<List<XiHanScheduledJobInfo>> GetAllJobsAsync()
    {
        try
        {
            var scheduler = await GetSchedulerAsync();
            var jobInfos = new List<XiHanScheduledJobInfo>();

            var jobGroups = await scheduler.GetJobGroupNames();
            foreach (var group in jobGroups)
            {
                var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(group));
                foreach (var jobKey in jobKeys)
                {
                    var jobInfo = await GetJobInfoInternalAsync(scheduler, jobKey);
                    if (jobInfo != null)
                    {
                        jobInfos.Add(jobInfo);
                    }
                }
            }

            return jobInfos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取所有调度任务失败");
            return [];
        }
    }

    /// <summary>
    /// 获取指定任务信息
    /// </summary>
    public async Task<XiHanScheduledJobInfo?> GetJobInfoAsync(string jobName, string jobGroup)
    {
        try
        {
            var scheduler = await GetSchedulerAsync();
            var jobKey = new JobKey(jobName, jobGroup);

            if (!await scheduler.CheckExists(jobKey))
            {
                return null;
            }

            return await GetJobInfoInternalAsync(scheduler, jobKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取调度任务信息失败: {JobGroup}.{JobName}", jobGroup, jobName);
            return null;
        }
    }

    /// <summary>
    /// 检查任务是否存在
    /// </summary>
    public async Task<bool> JobExistsAsync(string jobName, string jobGroup)
    {
        try
        {
            var scheduler = await GetSchedulerAsync();
            var jobKey = new JobKey(jobName, jobGroup);
            return await scheduler.CheckExists(jobKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查任务是否存在失败: {JobGroup}.{JobName}", jobGroup, jobName);
            return false;
        }
    }

    /// <summary>
    /// 启动调度器
    /// </summary>
    public async Task StartAsync()
    {
        try
        {
            if (!_options.Enabled)
            {
                _logger.LogWarning("调度器已禁用，跳过启动");
                return;
            }

            var scheduler = await GetSchedulerAsync();
            if (!scheduler.IsStarted)
            {
                await scheduler.Start();
                _logger.LogInformation("调度器已启动: {SchedulerName}", _options.SchedulerName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动调度器失败");
            throw;
        }
    }

    /// <summary>
    /// 停止调度器
    /// </summary>
    public async Task StopAsync()
    {
        try
        {
            if (_scheduler != null && !_scheduler.IsShutdown)
            {
                await _scheduler.Shutdown();
                _logger.LogInformation("调度器已停止: {SchedulerName}", _options.SchedulerName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止调度器失败");
            throw;
        }
    }

    /// <summary>
    /// 获取调度器状态
    /// </summary>
    public Task<bool> IsRunningAsync()
    {
        try
        {
            if (_scheduler == null)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(_scheduler.IsStarted && !_scheduler.IsShutdown);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取调度器状态失败");
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// 获取调度器实例
    /// </summary>
    private async Task<IScheduler> GetSchedulerAsync()
    {
        if (_scheduler != null)
        {
            return _scheduler;
        }

        var properties = new NameValueCollection
        {
            ["quartz.scheduler.instanceName"] = _options.SchedulerName,
            ["quartz.threadPool.threadCount"] = _options.ThreadPoolSize.ToString(),
            ["quartz.jobStore.type"] = "Quartz.Simpl.RAMJobStore, Quartz"
        };

        var schedulerFactory = new StdSchedulerFactory(properties);
        _scheduler = await schedulerFactory.GetScheduler();

        return _scheduler;
    }

    /// <summary>
    /// 内部方法：获取任务信息
    /// </summary>
    private async Task<XiHanScheduledJobInfo?> GetJobInfoInternalAsync(IScheduler scheduler, JobKey jobKey)
    {
        try
        {
            var jobDetail = await scheduler.GetJobDetail(jobKey);
            if (jobDetail == null)
            {
                return null;
            }

            var triggers = await scheduler.GetTriggersOfJob(jobKey);
            var trigger = triggers.FirstOrDefault();

            var state = trigger != null
                ? (await scheduler.GetTriggerState(trigger.Key)).ToString()
                : "Unknown";

            var cronTrigger = trigger as ICronTrigger;

            return new XiHanScheduledJobInfo
            {
                JobName = jobKey.Name,
                JobGroup = jobKey.Group,
                JobType = jobDetail.JobType.Name,
                CronExpression = cronTrigger?.CronExpressionString,
                Description = jobDetail.Description,
                State = state,
                PreviousFireTime = trigger?.GetPreviousFireTimeUtc()?.DateTime,
                NextFireTime = trigger?.GetNextFireTimeUtc()?.DateTime,
                Priority = trigger?.Priority ?? 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取任务详情失败: {JobKey}", jobKey);
            return null;
        }
    }
}
