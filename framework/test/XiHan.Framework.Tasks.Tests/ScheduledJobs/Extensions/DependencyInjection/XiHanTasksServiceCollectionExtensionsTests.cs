// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Configuration;
using XiHan.Framework.Tasks.ScheduledJobs.Executor;
using XiHan.Framework.Tasks.ScheduledJobs.Extensions.DependencyInjection;
using XiHan.Framework.Tasks.ScheduledJobs.Hosting;
using XiHan.Framework.Tasks.ScheduledJobs.Locking;
using XiHan.Framework.Tasks.ScheduledJobs.Models;
using XiHan.Framework.Tasks.ScheduledJobs.Monitoring;
using XiHan.Framework.Tasks.ScheduledJobs.Pipeline;
using XiHan.Framework.Tasks.ScheduledJobs.Scheduler;
using XiHan.Framework.Tasks.ScheduledJobs.Store;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Extensions.DependencyInjection;

/// <summary>
/// XiHanTasksServiceCollectionExtensions 服务注册测试
/// </summary>
/// <remarks>
/// 断言直接打在 ServiceDescriptor 上而不是解析实例：核心服务之间互相依赖（调度器依赖执行器、
/// 执行器依赖存储与中间件），解析会牵出整棵图并需要日志基础设施，描述符断言更聚焦也更稳。
/// </remarks>
public class XiHanTasksServiceCollectionExtensionsTests
{
    /// <summary>
    /// 服务集合为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void AddXiHanTasks_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => XiHanTasksServiceCollectionExtensions.AddXiHanTasks(null!, configureOptions: null));
    }

    /// <summary>
    /// 配置对象为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void AddXiHanTasks_WhenConfigurationIsNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddXiHanTasks((IConfiguration)null!));
    }

    /// <summary>
    /// 核心服务以单例注册，且实现类型符合默认约定
    /// </summary>
    [Theory]
    [InlineData(typeof(IJobStore), typeof(InMemoryJobStore))]
    [InlineData(typeof(IJobLockProvider), typeof(CachingJobLockProvider))]
    [InlineData(typeof(IJobScheduler), typeof(CompositeJobScheduler))]
    [InlineData(typeof(IJobExecutor), typeof(JobExecutor))]
    [InlineData(typeof(IJobEventPublisher), typeof(DefaultJobEventPublisher))]
    [InlineData(typeof(JobMetricsProvider), typeof(JobMetricsProvider))]
    public void AddXiHanTasks_RegistersCoreServicesAsSingletons(Type serviceType, Type implementationType)
    {
        IServiceCollection services = new ServiceCollection();

        services.AddXiHanTasks();

        var descriptor = Assert.Single(services, item => item.ServiceType == serviceType);
        Assert.Equal(implementationType, descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    /// <summary>
    /// 五个内置中间件按"日志 → 超时 → 锁 → 重试 → 度量"的顺序注册，顺序即洋葱层次
    /// </summary>
    [Fact]
    public void AddXiHanTasks_RegistersBuiltInMiddlewaresInDocumentedOrder()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddXiHanTasks();

        var implementationTypes = services
            .Where(item => item.ServiceType == typeof(IJobMiddleware))
            .Select(item => item.ImplementationType)
            .ToList();

        Assert.Equal(
            new[]
            {
                typeof(LoggingMiddleware),
                typeof(TimeoutMiddleware),
                typeof(LockMiddleware),
                typeof(RetryMiddleware),
                typeof(MetricsMiddleware)
            },
            implementationTypes);
    }

    /// <summary>
    /// 注册托管服务，把调度器的启停挂到主机生命周期上
    /// </summary>
    [Fact]
    public void AddXiHanTasks_RegistersHostedService()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddXiHanTasks();

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IHostedService));
        Assert.Equal(typeof(JobHostedService), descriptor.ImplementationType);
    }

    /// <summary>
    /// 返回的构建器指向同一个服务集合，可继续链式配置
    /// </summary>
    [Fact]
    public void AddXiHanTasks_ReturnsBuilderBoundToSameServiceCollection()
    {
        IServiceCollection services = new ServiceCollection();

        var builder = services.AddXiHanTasks();

        Assert.NotNull(builder);
        Assert.Same(services, builder.Services);
    }

    /// <summary>
    /// 传入配置委托时应用到选项上
    /// </summary>
    [Fact]
    public void AddXiHanTasks_WithConfigureDelegate_AppliesOptions()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddXiHanTasks(options =>
        {
            options.Enabled = false;
            options.HistoryRetentionDays = 5;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanJobOptions>>().Value;

        Assert.False(options.Enabled);
        Assert.Equal(5, options.HistoryRetentionDays);
    }

    /// <summary>
    /// 不传配置委托时保持选项默认值
    /// </summary>
    [Fact]
    public void AddXiHanTasks_WithoutConfigureDelegate_KeepsOptionDefaults()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddXiHanTasks();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanJobOptions>>().Value;

        Assert.True(options.Enabled);
        Assert.Equal(30, options.HistoryRetentionDays);
    }

    /// <summary>
    /// 传入配置对象时按约定配置节绑定选项
    /// </summary>
    [Fact]
    public void AddXiHanTasks_WithConfiguration_BindsFromConventionalSection()
    {
        IServiceCollection services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XiHan:Tasks:ScheduledJobs:Enabled"] = "false",
                ["XiHan:Tasks:ScheduledJobs:HistoryRetentionDays"] = "7",
                ["XiHan:Tasks:ScheduledJobs:NodeName"] = "node-from-config"
            })
            .Build();

        services.AddXiHanTasks(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanJobOptions>>().Value;

        Assert.False(options.Enabled);
        Assert.Equal(7, options.HistoryRetentionDays);
        Assert.Equal("node-from-config", options.NodeName);
    }

    /// <summary>
    /// 配置节缺失时退回默认值，不抛异常
    /// </summary>
    [Fact]
    public void AddXiHanTasks_WithEmptyConfiguration_FallsBackToDefaults()
    {
        IServiceCollection services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddXiHanTasks(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanJobOptions>>().Value;

        Assert.True(options.Enabled);
        Assert.Equal(30, options.HistoryRetentionDays);
    }

    /// <summary>
    /// 核心服务采用 TryAdd 语义：调用方已注册的自定义实现不会被默认实现覆盖
    /// </summary>
    [Fact]
    public void AddXiHanTasks_WhenCustomImplementationPreRegistered_DoesNotOverrideIt()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IJobStore, CustomJobStore>();

        services.AddXiHanTasks();

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IJobStore));
        Assert.Equal(typeof(CustomJobStore), descriptor.ImplementationType);
    }

    /// <summary>
    /// 重复调用时核心服务仍只有一份注册
    /// </summary>
    [Theory]
    [InlineData(typeof(IJobStore))]
    [InlineData(typeof(IJobLockProvider))]
    [InlineData(typeof(IJobScheduler))]
    [InlineData(typeof(IJobExecutor))]
    [InlineData(typeof(IJobEventPublisher))]
    [InlineData(typeof(JobMetricsProvider))]
    public void AddXiHanTasks_CalledTwice_KeepsCoreServicesSingle(Type serviceType)
    {
        IServiceCollection services = new ServiceCollection();

        services.AddXiHanTasks();
        services.AddXiHanTasks();

        Assert.Single(services, item => item.ServiceType == serviceType);
    }

    /// <summary>
    /// 显式切换到内存存储时覆盖默认注册
    /// </summary>
    [Fact]
    public void UseInMemoryStore_RegistersInMemoryJobStore()
    {
        IServiceCollection services = new ServiceCollection();

        var builder = services.AddXiHanTasks().UseInMemoryStore();

        var descriptors = services.Where(item => item.ServiceType == typeof(IJobStore)).ToList();
        Assert.Equal(typeof(InMemoryJobStore), descriptors[^1].ImplementationType);
        Assert.NotNull(builder);
    }

    /// <summary>
    /// 两个兼容旧 API 的锁扩展都指向统一的分布式锁适配器
    /// </summary>
    [Fact]
    public void UseInMemoryLockAndUseDistributedLock_BothRegisterCachingLockProvider()
    {
        IServiceCollection first = new ServiceCollection();
        IServiceCollection second = new ServiceCollection();

        first.AddXiHanTasks().UseInMemoryLock();
        second.AddXiHanTasks().UseDistributedLock();

        Assert.Equal(
            typeof(CachingJobLockProvider),
            first.Where(item => item.ServiceType == typeof(IJobLockProvider)).ToList()[^1].ImplementationType);
        Assert.Equal(
            typeof(CachingJobLockProvider),
            second.Where(item => item.ServiceType == typeof(IJobLockProvider)).ToList()[^1].ImplementationType);
    }

    /// <summary>
    /// 调用方自备的任务存储实现
    /// </summary>
    public sealed class CustomJobStore : IJobStore
    {
        /// <summary>
        /// 保存任务实例
        /// </summary>
        public Task SaveJobInstanceAsync(JobInstance jobInstance)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 更新任务实例状态
        /// </summary>
        public Task UpdateJobStatusAsync(string instanceId, JobStatus status)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 保存执行历史
        /// </summary>
        public Task SaveJobHistoryAsync(JobHistory history)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 获取任务实例
        /// </summary>
        public Task<JobInstance?> GetJobInstanceAsync(string instanceId)
        {
            return Task.FromResult<JobInstance?>(null);
        }

        /// <summary>
        /// 获取执行历史
        /// </summary>
        public Task<IReadOnlyList<JobHistory>> GetJobHistoryAsync(string jobName, int pageIndex = 1, int pageSize = 20)
        {
            return Task.FromResult<IReadOnlyList<JobHistory>>(new List<JobHistory>());
        }

        /// <summary>
        /// 获取运行中的任务实例
        /// </summary>
        public Task<IReadOnlyList<JobInstance>> GetRunningInstancesAsync(string jobName)
        {
            return Task.FromResult<IReadOnlyList<JobInstance>>(new List<JobInstance>());
        }

        /// <summary>
        /// 清理过期历史
        /// </summary>
        public Task CleanupHistoryAsync(int retentionDays)
        {
            return Task.CompletedTask;
        }
    }
}
