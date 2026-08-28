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
        // 选项容器必须无条件登记：原来只在 configureOptions 非空时才走 services.Configure，
        // 而 AddOptions 的注册（IOptions<>/IOptionsFactory<> 等开放泛型）恰恰是被 Configure 顺带带进来的。
        // 于是三条入口里只有「传配置节」与「传委托」两条能解析出 IOptions<XiHanJobOptions>，
        // 无参调用 AddXiHanTasks() 之后 GetRequiredService<IOptions<XiHanJobOptions>>() 直接抛
        // 「No service for type ... has been registered」——同一个扩展方法给出了三种不一致的容器状态。
        // AddOptions 幂等，且不会覆盖调用方后续追加的任何 Configure，安全地补齐默认值这条路径。
        services.AddOptions<XiHanJobOptions>();

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

        // 注册中间件（按顺序，顺序即洋葱层次）
        // 用 TryAddEnumerable 而不是 AddSingleton：核心服务都是 TryAddSingleton 幂等的，唯独这五条走的是
        // 普通 Add。AddXiHanTasks 被调用两次（模块装配 XiHanTasksModule 与业务侧显式调用叠加）就会得到
        // 10 条 IJobMiddleware 注册，整条执行管道被跑两遍。TryAddEnumerable 按"服务类型 + 实现类型"去重，
        // 既保持首次注册的顺序，又不影响调用方追加自定义中间件。
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IJobMiddleware, LoggingMiddleware>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IJobMiddleware, TimeoutMiddleware>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IJobMiddleware, LockMiddleware>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IJobMiddleware, RetryMiddleware>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IJobMiddleware, MetricsMiddleware>());

        // 注册后台服务（AddHostedService<T> 内部就是 TryAddEnumerable，重复调用天然幂等，无需额外处理）
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
