#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JobSchedulerExtensions.cs
// Guid:ea2f227a-2888-4cbb-b1b4-0493f9a2ebd6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 16:22:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Attributes;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Extensions;

/// <summary>
/// 任务调度器扩展方法
/// </summary>
public static class JobSchedulerExtensions
{
    /// <summary>
    /// 从程序集自动注册任务
    /// </summary>
    public static void RegisterJobsFromAssembly(this IJobScheduler scheduler, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(assembly);

        var jobTypes = assembly.GetTypes()
            .Where(t => typeof(IJob).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false })
            .ToList();

        foreach (var jobType in jobTypes)
        {
            var jobInfo = CreateJobInfoFromType(jobType);
            if (jobInfo != null)
            {
                scheduler.RegisterJob(jobInfo);
            }
        }
    }

    /// <summary>
    /// 注册 Cron 任务
    /// </summary>
    public static void RegisterCronJob<TJob>(
        this IJobScheduler scheduler,
        string jobName,
        string cronExpression,
        string? description = null,
        JobPriority priority = JobPriority.Normal) where TJob : IJob
    {
        var jobInfo = new JobInfo
        {
            JobName = jobName,
            Description = description,
            JobType = typeof(TJob),
            TriggerType = JobTriggerType.Cron,
            CronExpression = cronExpression,
            Priority = priority
        };

        scheduler.RegisterJob(jobInfo);
    }

    /// <summary>
    /// 注册间隔任务
    /// </summary>
    public static void RegisterIntervalJob<TJob>(
        this IJobScheduler scheduler,
        string jobName,
        TimeSpan interval,
        string? description = null,
        JobPriority priority = JobPriority.Normal) where TJob : IJob
    {
        var jobInfo = new JobInfo
        {
            JobName = jobName,
            Description = description,
            JobType = typeof(TJob),
            TriggerType = JobTriggerType.Interval,
            Interval = interval,
            Priority = priority
        };

        scheduler.RegisterJob(jobInfo);
    }

    /// <summary>
    /// 从类型创建任务信息
    /// </summary>
    private static JobInfo? CreateJobInfoFromType(Type jobType)
    {
        var nameAttr = jobType.GetCustomAttribute<JobNameAttribute>();
        if (nameAttr == null)
        {
            return null; // 没有 JobName 特性，跳过
        }

        var jobInfo = new JobInfo
        {
            JobName = nameAttr.Name,
            JobType = jobType
        };

        // 描述
        var descAttr = jobType.GetCustomAttribute<JobDescriptionAttribute>();
        if (descAttr != null)
        {
            jobInfo.Description = descAttr.Description;
        }

        // 调度配置
        var scheduleAttr = jobType.GetCustomAttribute<JobScheduleAttribute>();
        if (scheduleAttr != null)
        {
            jobInfo.TriggerType = scheduleAttr.TriggerType;
            jobInfo.CronExpression = scheduleAttr.CronExpression;

            if (scheduleAttr.IntervalSeconds > 0)
            {
                jobInfo.Interval = TimeSpan.FromSeconds(scheduleAttr.IntervalSeconds);
            }

            if (scheduleAttr.DelaySeconds > 0)
            {
                jobInfo.Delay = TimeSpan.FromSeconds(scheduleAttr.DelaySeconds);
            }
        }

        // 重试策略
        var retryAttr = jobType.GetCustomAttribute<JobRetryAttribute>();
        if (retryAttr != null)
        {
            jobInfo.RetryPolicy = new JobRetryPolicy
            {
                MaxRetryCount = retryAttr.MaxRetryCount,
                RetryIntervalMilliseconds = retryAttr.RetryIntervalMilliseconds,
                UseExponentialBackoff = retryAttr.UseExponentialBackoff
            };
        }

        // 并发控制
        var concurrentAttr = jobType.GetCustomAttribute<JobConcurrentAttribute>();
        if (concurrentAttr != null)
        {
            jobInfo.AllowConcurrent = concurrentAttr.AllowConcurrent;
        }

        // 超时配置
        var timeoutAttr = jobType.GetCustomAttribute<JobTimeoutAttribute>();
        if (timeoutAttr != null)
        {
            jobInfo.TimeoutMilliseconds = timeoutAttr.TimeoutMilliseconds;
        }

        // 优先级
        var priorityAttr = jobType.GetCustomAttribute<JobPriorityAttribute>();
        if (priorityAttr != null)
        {
            jobInfo.Priority = priorityAttr.Priority;
        }

        return jobInfo;
    }
}
