// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Extensions.DependencyInjection;
using XiHan.Framework.Tasks.BackgroundJobs.Options;
using XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;
using XiHan.Framework.Timing;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs.Extensions.DependencyInjection;

/// <summary>
/// 曦寒后台作业服务集合扩展测试
/// </summary>
/// <remarks>
/// 装配契约有三处容易出事：
/// 核心服务全部走 TryAdd（应用侧先注册的持久化存储不能被框架默认内存实现顶掉）、
/// 轮询 Worker 以托管服务身份单例登记、
/// 作业注册表由"注册钩子"事后填充（钩子只收集类型，真正的 AddJob 发生在解析 Options 的那一刻）。
/// 这里不断言服务描述符总数，只断言关心的服务各自登记了几条，避免把 DI 框架实现细节固化进用例。
/// </remarks>
public class XiHanBackgroundJobsServiceCollectionExtensionsTests
{
    /// <summary>
    /// 返回同一个服务集合以支持链式调用
    /// </summary>
    [Fact]
    public void AddXiHanBackgroundJobs_ReturnsSameServiceCollection()
    {
        var services = CreateServices();

        var returned = services.AddXiHanBackgroundJobs(CreateConfiguration());

        Assert.Same(services, returned);
    }

    /// <summary>
    /// 核心服务按约定的生命周期各登记一条
    /// </summary>
    [Fact]
    public void AddXiHanBackgroundJobs_RegistersCoreServicesWithExpectedLifetimes()
    {
        var services = CreateServices();

        services.AddXiHanBackgroundJobs(CreateConfiguration());

        AssertSingleDescriptor(services, typeof(IBackgroundJobSerializer), typeof(BackgroundJobSerializer), ServiceLifetime.Singleton);
        AssertSingleDescriptor(services, typeof(IBackgroundJobStore), typeof(InMemoryBackgroundJobStore), ServiceLifetime.Singleton);
        AssertSingleDescriptor(services, typeof(IBackgroundJobManager), typeof(BackgroundJobManager), ServiceLifetime.Transient);
        AssertSingleDescriptor(services, typeof(IBackgroundJobExecuter), typeof(BackgroundJobExecuter), ServiceLifetime.Transient);
    }

    /// <summary>
    /// 核心服务可以正常解析出来
    /// </summary>
    [Fact]
    public void AddXiHanBackgroundJobs_CoreServicesAreResolvable()
    {
        using var provider = BuildProvider();

        Assert.IsType<BackgroundJobSerializer>(provider.GetRequiredService<IBackgroundJobSerializer>());
        Assert.IsType<InMemoryBackgroundJobStore>(provider.GetRequiredService<IBackgroundJobStore>());
        Assert.IsType<BackgroundJobManager>(provider.GetRequiredService<IBackgroundJobManager>());
        Assert.IsType<BackgroundJobExecuter>(provider.GetRequiredService<IBackgroundJobExecuter>());
    }

    /// <summary>
    /// 存储用 TryAdd 注册：应用侧先注册的实现不会被框架默认内存实现顶掉
    /// </summary>
    [Fact]
    public void AddXiHanBackgroundJobs_DoesNotOverrideApplicationProvidedStore()
    {
        var services = CreateServices();
        services.AddSingleton<IBackgroundJobStore>(new RecordingBackgroundJobStore());

        services.AddXiHanBackgroundJobs(CreateConfiguration());

        using var provider = services.BuildServiceProvider();

        Assert.IsType<RecordingBackgroundJobStore>(provider.GetRequiredService<IBackgroundJobStore>());
    }

    /// <summary>
    /// 轮询 Worker 以托管服务身份登记为单例
    /// </summary>
    [Fact]
    public void AddXiHanBackgroundJobs_RegistersWorkerAsHostedService()
    {
        var services = CreateServices();

        services.AddXiHanBackgroundJobs(CreateConfiguration());

        var hostedDescriptors = services
            .Where(x => x.ServiceType == typeof(IHostedService))
            .ToArray();

        var workerDescriptor = Assert.Single(hostedDescriptors, x => x.ImplementationType == typeof(BackgroundJobWorker));
        Assert.Equal(ServiceLifetime.Singleton, workerDescriptor.Lifetime);
    }

    /// <summary>
    /// Worker 选项绑定到约定的配置节
    /// </summary>
    [Fact]
    public void AddXiHanBackgroundJobs_BindsWorkerOptionsFromConfiguredSection()
    {
        var settings = new Dictionary<string, string?>
        {
            ["XiHan:BackgroundJobs:IsJobExecutionEnabled"] = "false",
            ["XiHan:BackgroundJobs:JobPollPeriodMilliseconds"] = "1234",
            ["XiHan:BackgroundJobs:ApplicationName"] = "order-service",
            ["XiHan:BackgroundJobs:DefaultWaitFactor"] = "3.5"
        };

        using var provider = BuildProvider(settings);
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<BackgroundJobWorkerOptions>>().Value;

        Assert.False(options.IsJobExecutionEnabled);
        Assert.Equal(1234, options.JobPollPeriodMilliseconds);
        Assert.Equal("order-service", options.ApplicationName);
        Assert.Equal(3.5, options.DefaultWaitFactor);
    }

    /// <summary>
    /// 配置节缺省时 Worker 选项保持默认值
    /// </summary>
    [Fact]
    public void AddXiHanBackgroundJobs_WhenSectionMissing_KeepsWorkerOptionDefaults()
    {
        using var provider = BuildProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<BackgroundJobWorkerOptions>>().Value;

        Assert.True(options.IsJobExecutionEnabled);
        Assert.Equal(5000, options.JobPollPeriodMilliseconds);
    }

    /// <summary>
    /// 注册钩子只收集实现了作业接口的类型，其余类型被忽略
    /// </summary>
    [Fact]
    public void AddXiHanBackgroundJobs_JobDiscoveryOnlyCollectsBackgroundJobs()
    {
        var services = CreateServices();
        services.AddXiHanBackgroundJobs(CreateConfiguration());

        foreach (var action in services.GetRegistrationActionList())
        {
            action(new OnServiceRegistredContext(typeof(NamedArgsJob), typeof(NamedArgsJob)));
            action(new OnServiceRegistredContext(typeof(NotABackgroundJob), typeof(NotABackgroundJob)));
            action(new OnServiceRegistredContext(typeof(AbstractSampleJob), typeof(AbstractSampleJob)));
        }

        using var provider = services.BuildServiceProvider();
        var jobOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<BackgroundJobOptions>>().Value;

        var configuration = Assert.Single(jobOptions.GetJobs());
        Assert.Equal(typeof(NamedArgsJob), configuration.JobType);
        Assert.Equal("xihan-tests-named-args", configuration.JobName);
        Assert.NotNull(jobOptions.GetJobByArgsOrNull(typeof(NamedJobArgs)));
    }

    /// <summary>
    /// 未经注册钩子触发时注册表为空（自动发现依赖框架的注册管线）
    /// </summary>
    [Fact]
    public void AddXiHanBackgroundJobs_WhenNoRegistrationHappened_JobRegistryIsEmpty()
    {
        using var provider = BuildProvider();

        var jobOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<BackgroundJobOptions>>().Value;

        Assert.Empty(jobOptions.GetJobs());
    }

    /// <summary>
    /// 切换 Redis 存储会替换掉默认内存实现，只留一条描述符
    /// </summary>
    [Fact]
    public void UseRedisBackgroundJobStore_ReplacesDefaultStoreDescriptor()
    {
        var services = CreateServices();
        services.AddXiHanBackgroundJobs(CreateConfiguration());

        services.UseRedisBackgroundJobStore();

        AssertSingleDescriptor(services, typeof(IBackgroundJobStore), typeof(RedisBackgroundJobStore), ServiceLifetime.Singleton);
    }

    /// <summary>
    /// 切换 Redis 存储时可自定义存储选项
    /// </summary>
    [Fact]
    public void UseRedisBackgroundJobStore_AppliesCustomOptions()
    {
        var services = CreateServices();
        services.AddXiHanBackgroundJobs(CreateConfiguration());

        services.UseRedisBackgroundJobStore(options =>
        {
            options.KeyPrefix = "App:Jobs";
            options.FetchMultiplier = 8;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RedisBackgroundJobStoreOptions>>().Value;

        Assert.Equal("App:Jobs", options.KeyPrefix);
        Assert.Equal(8, options.FetchMultiplier);
    }

    /// <summary>
    /// 不传自定义配置时 Redis 存储选项保持默认值
    /// </summary>
    [Fact]
    public void UseRedisBackgroundJobStore_WithoutConfigure_KeepsDefaults()
    {
        var services = CreateServices();
        services.AddXiHanBackgroundJobs(CreateConfiguration());

        services.UseRedisBackgroundJobStore();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RedisBackgroundJobStoreOptions>>().Value;

        Assert.Equal("XiHan:BackgroundJobs", options.KeyPrefix);
        Assert.Equal(4, options.FetchMultiplier);
    }

    /// <summary>
    /// 断言某个服务类型只登记了一条描述符，且实现类型与生命周期符合预期
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="serviceType">服务类型</param>
    /// <param name="implementationType">期望的实现类型</param>
    /// <param name="lifetime">期望的生命周期</param>
    private static void AssertSingleDescriptor(IServiceCollection services, Type serviceType, Type implementationType, ServiceLifetime lifetime)
    {
        var descriptor = Assert.Single(services.Where(x => x.ServiceType == serviceType));

        Assert.Equal(implementationType, descriptor.ImplementationType);
        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    /// <summary>
    /// 创建带最小依赖的服务集合（后台作业本身不负责注册日志、时钟与租户上下文）
    /// </summary>
    /// <returns>服务集合</returns>
    private static IServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock>(new FakeClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        services.AddSingleton<ICurrentTenant>(new FakeCurrentTenant());
        return services;
    }

    /// <summary>
    /// 创建内存配置
    /// </summary>
    /// <param name="settings">配置项</param>
    /// <returns>配置</returns>
    private static IConfiguration CreateConfiguration(Dictionary<string, string?>? settings = null)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? new Dictionary<string, string?>())
            .Build();
    }

    /// <summary>
    /// 构建服务提供器
    /// </summary>
    /// <param name="settings">配置项</param>
    /// <returns>服务提供器</returns>
    private static ServiceProvider BuildProvider(Dictionary<string, string?>? settings = null)
    {
        var services = CreateServices();
        services.AddXiHanBackgroundJobs(CreateConfiguration(settings));
        return services.BuildServiceProvider();
    }
}
