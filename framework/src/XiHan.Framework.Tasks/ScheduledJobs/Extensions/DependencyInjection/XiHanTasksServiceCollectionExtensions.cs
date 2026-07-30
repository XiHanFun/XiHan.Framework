// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
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

namespace XiHan.Framework.Tasks.ScheduledJobs.Extensions.DependencyInjection;

/// <summary>
/// 服务集合扩展方法
/// </summary>
public static class XiHanTasksServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒任务调度服务（从配置文件绑定选项）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>任务构建器</returns>
    public static XiHanJobBuilder AddXiHanTasks(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<XiHanJobOptions>(configuration.GetSection(XiHanJobOptions.SectionName));
        return services.AddXiHanTasks(configureOptions: null);
    }

    /// <summary>
    /// 添加曦寒任务调度服务
    /// </summary>
    public static XiHanJobBuilder AddXiHanTasks(this IServiceCollection services, Action<XiHanJobOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // 配置选项
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        // 注册核心服务
        services.TryAddSingleton<IJobStore, InMemoryJobStore>();
        // 任务锁：复用 Caching 模块统一的分布式锁（Redis 跨实例 / 进程内回退由其按 Redis 配置自动选择，XiHanJobOptions.EnableDistributedLock 已不再需要）
        services.TryAddSingleton<IJobLockProvider, CachingJobLockProvider>();
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
    /// 使用任务锁（兼容旧 API）。实际锁后端（Redis 跨实例 / 进程内回退）由 Caching 统一的分布式锁按 Redis 配置自动选择。
    /// </summary>
    public static XiHanJobBuilder UseInMemoryLock(this XiHanJobBuilder builder)
    {
        return builder.UseLockProvider<CachingJobLockProvider>();
    }

    /// <summary>
    /// 使用任务锁（兼容旧 API）。实际锁后端（Redis 跨实例 / 进程内回退）由 Caching 统一的分布式锁按 Redis 配置自动选择。
    /// </summary>
    /// <param name="builder">任务构建器</param>
    /// <returns>任务构建器</returns>
    public static XiHanJobBuilder UseDistributedLock(this XiHanJobBuilder builder)
    {
        return builder.UseLockProvider<CachingJobLockProvider>();
    }
}
