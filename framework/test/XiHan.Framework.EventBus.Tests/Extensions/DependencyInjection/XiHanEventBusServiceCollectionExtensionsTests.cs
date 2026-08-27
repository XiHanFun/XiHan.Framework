// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.EventBus.Distributed;
using XiHan.Framework.EventBus.Extensions.DependencyInjection;
using XiHan.Framework.EventBus.Local;
using XiHan.Framework.EventBus.Tests.Fakes;

namespace XiHan.Framework.EventBus.Tests.Extensions.DependencyInjection;

/// <summary>
/// 事件总线服务注册扩展测试
/// </summary>
/// <remarks>
/// 这是模块装配的唯一入口：默认事件盒实现、后台处理服务、以及「按接口自动归类事件处理器」三件事都在这里落地。
/// 断言只针对关心的服务各登记了几条，不锁死描述符总数，避免依赖注入实现细节变动导致误报。
/// </remarks>
public class XiHanEventBusServiceCollectionExtensionsTests
{
    /// <summary>
    /// 默认事件盒以单例注册，接口与实现类解析到同一个实例
    /// </summary>
    [Fact]
    public void AddXiHanEventBus_RegistersInMemoryEventBoxesAsSingletons()
    {
        var services = new ServiceCollection();
        services.AddXiHanEventBus(BuildConfiguration());
        using var provider = services.BuildServiceProvider();

        var outbox = provider.GetRequiredService<InMemoryEventOutbox>();
        var inbox = provider.GetRequiredService<InMemoryEventInbox>();

        Assert.Same(outbox, provider.GetRequiredService<InMemoryEventOutbox>());
        Assert.Same(inbox, provider.GetRequiredService<InMemoryEventInbox>());
        Assert.Same(outbox, provider.GetRequiredService<IEventOutbox>());
        Assert.Same(inbox, provider.GetRequiredService<IEventInbox>());
    }

    /// <summary>
    /// 默认收发件箱配置指向内存实现并落在默认数据库上
    /// </summary>
    [Fact]
    public void AddXiHanEventBus_ConfiguresDefaultEventBoxes()
    {
        var services = new ServiceCollection();
        services.AddXiHanEventBus(BuildConfiguration());
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<XiHanDistributedEventBusOptions>>().Value;

        Assert.True(options.Outboxes.ContainsKey("Default"));
        Assert.True(options.Inboxes.ContainsKey("Default"));
        Assert.Equal(typeof(InMemoryEventOutbox), options.Outboxes["Default"].ImplementationType);
        Assert.Equal(typeof(InMemoryEventInbox), options.Inboxes["Default"].ImplementationType);
        Assert.Equal("Default", options.Outboxes["Default"].DatabaseName);
        Assert.Equal("Default", options.Inboxes["Default"].DatabaseName);
    }

    /// <summary>
    /// 事件盒后台处理配置从约定的配置节绑定
    /// </summary>
    [Fact]
    public void AddXiHanEventBus_BindsProcessingOptionsFromConfiguration()
    {
        var services = new ServiceCollection();
        services.AddXiHanEventBus(BuildConfiguration(new Dictionary<string, string?>
        {
            ["XiHan:EventBus:EventBoxes:PollingIntervalMilliseconds"] = "500",
            ["XiHan:EventBus:EventBoxes:MaxInboxRetryCount"] = "9"
        }));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<EventBoxProcessingOptions>>().Value;

        Assert.Equal(500, options.PollingIntervalMilliseconds);
        Assert.Equal(9, options.MaxInboxRetryCount);
        // 未出现在配置里的项保持代码默认值
        Assert.Equal(100, options.OutboxBatchSize);
    }

    /// <summary>
    /// 收发件箱的后台处理服务各注册一条且为单例
    /// </summary>
    [Fact]
    public void AddXiHanEventBus_RegistersBothEventBoxHostedServices()
    {
        var services = new ServiceCollection();
        services.AddXiHanEventBus(BuildConfiguration());

        var hostedServices = services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService)).ToList();

        Assert.Contains(hostedServices, descriptor => descriptor.ImplementationType == typeof(EventBoxOutboxSenderHostedService));
        Assert.Contains(hostedServices, descriptor => descriptor.ImplementationType == typeof(EventBoxInboxProcessorHostedService));
        Assert.All(hostedServices, descriptor => Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime));
    }

    /// <summary>
    /// 重复调用不会把同一批服务登记两遍
    /// </summary>
    [Fact]
    public void AddXiHanEventBus_CalledTwice_DoesNotDuplicateRegistrations()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration();

        services.AddXiHanEventBus(configuration);
        services.AddXiHanEventBus(configuration);

        Assert.Equal(2, CountDescriptors(services, typeof(IHostedService)));
        Assert.Equal(1, CountDescriptors(services, typeof(InMemoryEventOutbox)));
        Assert.Equal(1, CountDescriptors(services, typeof(InMemoryEventInbox)));
        Assert.Equal(1, CountDescriptors(services, typeof(IEventOutbox)));
        Assert.Equal(1, CountDescriptors(services, typeof(IEventInbox)));
    }

    /// <summary>
    /// 扩展方法返回同一个服务集合以支持链式调用
    /// </summary>
    [Fact]
    public void AddXiHanEventBus_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddXiHanEventBus(BuildConfiguration()));
    }

    /// <summary>
    /// 注册回调会把本地处理器归入本地事件总线选项
    /// </summary>
    /// <remarks>
    /// 真实流程由模块系统的约定注册器触发回调，这里直接回放回调以验证归类逻辑本身。
    /// </remarks>
    [Fact]
    public void AddXiHanEventBus_ClassifiesLocalHandlers()
    {
        var services = new ServiceCollection();
        services.AddXiHanEventBus(BuildConfiguration());
        ReplayRegistration(services, typeof(ILocalEventHandler<PlainNoticeEvent>), typeof(RecordingLocalHandler<PlainNoticeEvent>));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<XiHanLocalEventBusOptions>>().Value;

        Assert.Contains(typeof(RecordingLocalHandler<PlainNoticeEvent>), options.Handlers);
    }

    /// <summary>
    /// 注册回调会把分布式处理器归入分布式事件总线选项
    /// </summary>
    [Fact]
    public void AddXiHanEventBus_ClassifiesDistributedHandlers()
    {
        var services = new ServiceCollection();
        services.AddXiHanEventBus(BuildConfiguration());
        ReplayRegistration(services, typeof(IDistributedEventHandler<NamedNoticeEvent>), typeof(RecordingDistributedHandler<NamedNoticeEvent>));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<XiHanDistributedEventBusOptions>>().Value;

        Assert.Contains(typeof(RecordingDistributedHandler<NamedNoticeEvent>), options.Handlers);
    }

    /// <summary>
    /// 同时实现两种处理器接口的类型会被同时归入两侧
    /// </summary>
    [Fact]
    public void AddXiHanEventBus_ClassifiesDualChannelHandlerIntoBothSides()
    {
        var services = new ServiceCollection();
        services.AddXiHanEventBus(BuildConfiguration());
        ReplayRegistration(services, typeof(ILocalEventHandler<PlainNoticeEvent>), typeof(DualChannelHandler));
        using var provider = services.BuildServiceProvider();

        var localOptions = provider.GetRequiredService<IOptions<XiHanLocalEventBusOptions>>().Value;
        var distributedOptions = provider.GetRequiredService<IOptions<XiHanDistributedEventBusOptions>>().Value;

        Assert.Contains(typeof(DualChannelHandler), localOptions.Handlers);
        Assert.Contains(typeof(DualChannelHandler), distributedOptions.Handlers);
    }

    /// <summary>
    /// 非事件处理器类型不会被误收进任何一侧
    /// </summary>
    [Fact]
    public void AddXiHanEventBus_IgnoresNonHandlerTypes()
    {
        var services = new ServiceCollection();
        services.AddXiHanEventBus(BuildConfiguration());
        ReplayRegistration(services, typeof(ScopedProbe), typeof(ScopedProbe));
        using var provider = services.BuildServiceProvider();

        var localOptions = provider.GetRequiredService<IOptions<XiHanLocalEventBusOptions>>().Value;
        var distributedOptions = provider.GetRequiredService<IOptions<XiHanDistributedEventBusOptions>>().Value;

        Assert.DoesNotContain(typeof(ScopedProbe), localOptions.Handlers);
        Assert.DoesNotContain(typeof(ScopedProbe), distributedOptions.Handlers);
    }

    /// <summary>
    /// 统计指定服务类型的描述符条数
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="serviceType">服务类型</param>
    /// <returns>描述符条数</returns>
    private static int CountDescriptors(IServiceCollection services, Type serviceType)
    {
        var matched = services.Where(descriptor => descriptor.ServiceType == serviceType).ToList();

        return matched.Count;
    }

    /// <summary>
    /// 回放一次服务注册回调
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="serviceType">服务类型</param>
    /// <param name="implementationType">实现类型</param>
    private static void ReplayRegistration(IServiceCollection services, Type serviceType, Type implementationType)
    {
        var context = new OnServiceRegistredContext(serviceType, implementationType);
        foreach (var action in services.GetRegistrationActionList())
        {
            action(context);
        }
    }

    /// <summary>
    /// 构造测试用配置
    /// </summary>
    /// <param name="values">配置项</param>
    /// <returns>配置</returns>
    private static IConfiguration BuildConfiguration(Dictionary<string, string?>? values = null)
    {
        var initialData = values ?? new Dictionary<string, string?>();

        return new ConfigurationBuilder()
            .AddInMemoryCollection(initialData)
            .Build();
    }
}
