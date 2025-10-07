#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ServiceCollectionExtensions
// Guid:5ee4588e-3755-4527-88cf-40471dc706bb
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 17:54:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Configuration;
using XiHan.Framework.Tasks.ScheduledJobs.Executor;
using XiHan.Framework.Tasks.ScheduledJobs.Hosting;
using XiHan.Framework.Tasks.ScheduledJobs.Locking;
using XiHan.Framework.Tasks.ScheduledJobs.Monitoring;
using XiHan.Framework.Tasks.ScheduledJobs.Pipeline;
using XiHan.Framework.Tasks.ScheduledJobs.Scheduler;
using XiHan.Framework.Tasks.ScheduledJobs.Store;

namespace XiHan.Framework.Tasks.ScheduledJobs.Extensions;

/// <summary>
/// 服务集合扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒任务调度服务
    /// </summary>
    public static XiHanJobBuilder AddXiHanJobs(
        this IServiceCollection services,
        Action<XiHanJobOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // 配置选项
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        // 注册核心服务
        services.TryAddSingleton<IJobStore, InMemoryJobStore>();
        services.TryAddSingleton<IJobLockProvider, InMemoryLockProvider>();
        services.TryAddSingleton<IJobScheduler, CompositeJobScheduler>();
        services.TryAddSingleton<IJobExecutor, JobExecutor>();
        services.TryAddSingleton<JobMetricsProvider>();
        services.TryAddSingleton<IJobEventPublisher, DefaultJobEventPublisher>();

        // 注册中间件（按顺序）
        services.AddSingleton<IJobMiddleware, LoggingMiddleware>();
        services.AddSingleton<IJobMiddleware, TimeoutMiddleware>();
        services.AddSingleton<IJobMiddleware, LockMiddleware>();
        services.AddSingleton<IJobMiddleware, RetryMiddleware>();
        services.AddSingleton<IJobMiddleware, MetricsMiddleware>();

        // 注册后台服务
        services.AddHostedService<JobHostedService>();

        return new XiHanJobBuilder(services);
    }

    /// <summary>
    /// 使用内存存储
    /// </summary>
    public static XiHanJobBuilder UseInMemoryStore(this XiHanJobBuilder builder)
    {
        return builder.UseStore<InMemoryJobStore>();
    }

    /// <summary>
    /// 使用内存锁
    /// </summary>
    public static XiHanJobBuilder UseInMemoryLock(this XiHanJobBuilder builder)
    {
        return builder.UseLockProvider<InMemoryLockProvider>();
    }
}
