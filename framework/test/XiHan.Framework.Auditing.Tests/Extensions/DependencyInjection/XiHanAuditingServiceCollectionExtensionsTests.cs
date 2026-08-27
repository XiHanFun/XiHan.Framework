// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using XiHan.Framework.Auditing.Extensions.DependencyInjection;
using XiHan.Framework.Auditing.Options;
using XiHan.Framework.Auditing.Pipelines;
using XiHan.Framework.Auditing.Queues;
using XiHan.Framework.Auditing.Tests.Fakes;
using XiHan.Framework.Auditing.Workers;
using XiHan.Framework.Auditing.Writers;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Security.Users;

namespace XiHan.Framework.Auditing.Tests.Extensions.DependencyInjection;

/// <summary>
/// 曦寒审计日志服务集合扩展测试
/// </summary>
/// <remarks>
/// 装配契约有四点值得单独验证：
/// 队列是开放泛型单例（五类记录共用一份注册，且必须跨作用域同一实例，否则生产者与消费者会各拿一条队列）、
/// 管道是 Scoped（随请求生命周期）、后台消费者恰好五个且为单例、
/// 写入器与实体审计契约用 TryAdd 注册（应用侧先注册的实现不能被框架默认空实现顶掉）。
/// <para>
/// 这里额外调用了 <c>AddLogging()</c>：管道依赖 <c>ILogger&lt;T&gt;</c>，由宿主统一提供，
/// <c>AddXiHanAuditing</c> 本身不负责注册日志基础设施。
/// </para>
/// </remarks>
public class XiHanAuditingServiceCollectionExtensionsTests
{
    /// <summary>
    /// 返回同一个服务集合以支持链式调用
    /// </summary>
    [Fact]
    public void AddXiHanAuditing_ReturnsSameServiceCollection()
    {
        var services = CreateServices();

        var returned = services.AddXiHanAuditing(CreateConfiguration());

        Assert.Same(services, returned);
    }

    /// <summary>
    /// 日志队列按开放泛型注册为单例，跨作用域取到同一实例
    /// </summary>
    [Fact]
    public void AddXiHanAuditing_RegistersLogQueueAsOpenGenericSingleton()
    {
        using var provider = BuildProvider();

        var fromRoot = provider.GetRequiredService<ILogQueue<AccessLogRecord>>();
        using var scope = provider.CreateScope();
        var fromScope = scope.ServiceProvider.GetRequiredService<ILogQueue<AccessLogRecord>>();

        Assert.IsType<ChannelLogQueue<AccessLogRecord>>(fromRoot);
        Assert.Same(fromRoot, fromScope);

        // 另一类记录闭合出的是独立队列实例
        var loginQueue = provider.GetRequiredService<ILogQueue<LoginLogRecord>>();
        Assert.IsType<ChannelLogQueue<LoginLogRecord>>(loginQueue);
        Assert.NotSame(fromRoot, loginQueue);
    }

    /// <summary>
    /// 五个采集管道均按 Scoped 注册，作用域内复用、跨作用域隔离
    /// </summary>
    [Fact]
    public void AddXiHanAuditing_RegistersPipelinesAsScoped()
    {
        using var provider = BuildProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        var access = first.ServiceProvider.GetRequiredService<IAccessLogPipeline>();

        Assert.IsType<AccessLogPipeline>(access);
        Assert.Same(access, first.ServiceProvider.GetRequiredService<IAccessLogPipeline>());
        Assert.NotSame(access, second.ServiceProvider.GetRequiredService<IAccessLogPipeline>());

        Assert.IsType<OperationLogPipeline>(first.ServiceProvider.GetRequiredService<IOperationLogPipeline>());
        Assert.IsType<ExceptionLogPipeline>(first.ServiceProvider.GetRequiredService<IExceptionLogPipeline>());
        Assert.IsType<ApiLogPipeline>(first.ServiceProvider.GetRequiredService<IApiLogPipeline>());
        Assert.IsType<LoginLogPipeline>(first.ServiceProvider.GetRequiredService<ILoginLogPipeline>());
    }

    /// <summary>
    /// 恰好注册五个后台消费者，且均为单例
    /// </summary>
    [Fact]
    public void AddXiHanAuditing_RegistersFiveSingletonHostedServices()
    {
        var services = CreateServices();
        services.AddXiHanAuditing(CreateConfiguration());

        var hostedDescriptors = services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService)).ToArray();
        var implementationTypes = hostedDescriptors
            .Select(descriptor => descriptor.ImplementationType)
            .OfType<Type>()
            .ToArray();

        Assert.Equal(5, hostedDescriptors.Length);
        Assert.Contains(typeof(AccessLogQueueWorker), implementationTypes);
        Assert.Contains(typeof(OperationLogQueueWorker), implementationTypes);
        Assert.Contains(typeof(ExceptionLogQueueWorker), implementationTypes);
        Assert.Contains(typeof(ApiLogQueueWorker), implementationTypes);
        Assert.Contains(typeof(LoginLogQueueWorker), implementationTypes);

        Assert.All(hostedDescriptors, descriptor => Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime));
    }

    /// <summary>
    /// 未注册自定义写入器时全部回落到空实现
    /// </summary>
    [Fact]
    public void AddXiHanAuditing_WritersDefaultToNullImplementations()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var scoped = scope.ServiceProvider;

        Assert.IsType<NullAccessLogWriter>(scoped.GetRequiredService<IAccessLogWriter>());
        Assert.IsType<NullOperationLogWriter>(scoped.GetRequiredService<IOperationLogWriter>());
        Assert.IsType<NullExceptionLogWriter>(scoped.GetRequiredService<IExceptionLogWriter>());
        Assert.IsType<NullApiLogWriter>(scoped.GetRequiredService<IApiLogWriter>());
        Assert.IsType<NullLoginLogWriter>(scoped.GetRequiredService<ILoginLogWriter>());
    }

    /// <summary>
    /// 应用侧已注册的写入器不会被空实现顶掉（TryAdd 语义）
    /// </summary>
    [Fact]
    public void AddXiHanAuditing_DoesNotOverrideAlreadyRegisteredWriter()
    {
        var services = CreateServices();
        services.AddScoped<IAccessLogWriter, RecordingAccessLogWriter>();
        services.AddXiHanAuditing(CreateConfiguration());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<RecordingAccessLogWriter>(scope.ServiceProvider.GetRequiredService<IAccessLogWriter>());

        // 未被应用侧接管的其它写入器仍是空实现
        Assert.IsType<NullOperationLogWriter>(scope.ServiceProvider.GetRequiredService<IOperationLogWriter>());
    }

    /// <summary>
    /// 实体变更审计注册默认上下文提供器与空差异写入器
    /// </summary>
    [Fact]
    public void AddXiHanAuditing_RegistersEntityAuditDefaults()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<DefaultEntityAuditContextProvider>(
            scope.ServiceProvider.GetRequiredService<IEntityAuditContextProvider>());
        Assert.IsType<NullEntityDiffLogWriter>(
            scope.ServiceProvider.GetRequiredService<IEntityDiffLogWriter>());
    }

    /// <summary>
    /// 选项从约定的配置节绑定，未配置项保持默认
    /// </summary>
    [Fact]
    public void AddXiHanAuditing_BindsOptionsFromConfigurationSection()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["XiHan:Auditing:LogQueue:EnableAccessLogQueue"] = "true",
            ["XiHan:Auditing:LogQueue:EnableLoginLogQueue"] = "true",
            ["XiHan:Auditing:LogQueue:DropOnFull"] = "true",
            ["XiHan:Auditing:LogQueue:QueueCapacity"] = "256",
            ["XiHan:Auditing:LogQueue:BatchSize"] = "16",
            ["XiHan:Auditing:LogQueue:BatchDelayMilliseconds"] = "50"
        });

        var options = provider.GetRequiredService<IOptions<XiHanAuditingLogQueueOptions>>().Value;

        Assert.True(options.EnableAccessLogQueue);
        Assert.True(options.EnableLoginLogQueue);
        Assert.True(options.DropOnFull);
        Assert.Equal(256, options.QueueCapacity);
        Assert.Equal(16, options.BatchSize);
        Assert.Equal(50, options.BatchDelayMilliseconds);

        Assert.False(options.EnableApiLogQueue);
        Assert.False(options.EnableOperationLogQueue);
        Assert.False(options.EnableExceptionLogQueue);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();

        // 管道依赖 ILogger<T>，由宿主提供；实体审计上下文提供器依赖当前用户与当前租户
        services.AddLogging();
        services.AddSingleton<ICurrentUser>(new FakeCurrentUser());
        services.AddSingleton<ICurrentTenant>(new FakeCurrentTenant());

        return services;
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?>? settings = null)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? new Dictionary<string, string?>())
            .Build();
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?>? settings = null)
    {
        var services = CreateServices();
        services.AddXiHanAuditing(CreateConfiguration(settings));
        return services.BuildServiceProvider();
    }
}
