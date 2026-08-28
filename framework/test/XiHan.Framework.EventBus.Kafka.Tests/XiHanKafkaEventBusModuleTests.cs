// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.EventBus.Abstractions.Distributed;

namespace XiHan.Framework.EventBus.Kafka.Tests;

/// <summary>
/// Kafka 分布式事件总线模块测试
/// </summary>
/// <remarks>
/// 模块只做两件事：把 <c>XiHan:EventBus:Kafka</c> 配置节绑到选项上，以及在应用初始化阶段拿到
/// <see cref="KafkaDistributedEventBus"/> 并建立连接。总线实现本身走的是约定注册
/// （<c>ISingletonDependency</c> + <c>ExposeServices</c>），模块不显式注册它——
/// 这条边界很容易在重构中被打破，所以正反两面都锁住。
/// </remarks>
public class XiHanKafkaEventBusModuleTests
{
    /// <summary>
    /// 模块继承框架模块基类，才能被模块加载器识别
    /// </summary>
    [Fact]
    public void Module_IsXiHanModule()
    {
        Assert.True(typeof(XiHanKafkaEventBusModule).IsAssignableTo(typeof(XiHanModule)));
        Assert.True(typeof(XiHanKafkaEventBusModule).IsAssignableTo(typeof(IXiHanModule)));
    }

    /// <summary>
    /// 模块仅依赖事件总线模块
    /// </summary>
    /// <remarks>
    /// 依赖声明决定了 <c>PreConfigureServices</c> 阶段 <c>AddXiHanEventBus</c> 是否先于本模块跑完；
    /// 少了这条依赖，事件盒与处理器登记都不会就绪，故障要到运行期才暴露。
    /// </remarks>
    [Fact]
    public void Module_DependsOnEventBusModuleOnly()
    {
        var attribute = Assert.Single(typeof(XiHanKafkaEventBusModule).GetCustomAttributes<DependsOnAttribute>(false));

        Assert.Equal(typeof(XiHanEventBusModule), Assert.Single(attribute.GetDependedTypes()));
    }

    /// <summary>
    /// 服务配置把 Kafka 配置节绑定到选项
    /// </summary>
    [Fact]
    public void ConfigureServices_BindsOptionsFromKafkaSection()
    {
        var context = CreateContext(new Dictionary<string, string?>
        {
            [$"{XiHanKafkaEventBusOptions.SectionName}:BootstrapServers"] = "kafka-1:9092",
            [$"{XiHanKafkaEventBusOptions.SectionName}:TopicName"] = "MyApp.Events",
            [$"{XiHanKafkaEventBusOptions.SectionName}:GroupId"] = "MyApp.Consumers",
            [$"{XiHanKafkaEventBusOptions.SectionName}:EnsureTopicExists"] = "false"
        });

        new XiHanKafkaEventBusModule().ConfigureServices(context);

        var options = ResolveOptions(context);

        Assert.Equal("kafka-1:9092", options.BootstrapServers);
        Assert.Equal("MyApp.Events", options.TopicName);
        Assert.Equal("MyApp.Consumers", options.GroupId);
        Assert.False(options.EnsureTopicExists);
    }

    /// <summary>
    /// 配置源里没有 Kafka 配置节时保留默认选项
    /// </summary>
    [Fact]
    public void ConfigureServices_WhenSectionAbsent_KeepsDefaultOptions()
    {
        var context = CreateContext([]);

        new XiHanKafkaEventBusModule().ConfigureServices(context);

        var options = ResolveOptions(context);

        Assert.Equal("localhost:9092", options.BootstrapServers);
        Assert.Equal("Default.EventBus", options.TopicName);
        Assert.True(options.EnsureTopicExists);
    }

    /// <summary>
    /// 异步入口与同步入口行为一致
    /// </summary>
    [Fact]
    public async Task ConfigureServicesAsync_DelegatesToSyncOverload()
    {
        var context = CreateContext(new Dictionary<string, string?>
        {
            [$"{XiHanKafkaEventBusOptions.SectionName}:TopicName"] = "Async.Events"
        });

        await new XiHanKafkaEventBusModule().ConfigureServicesAsync(context);

        Assert.Equal("Async.Events", ResolveOptions(context).TopicName);
    }

    /// <summary>
    /// 容器里没有配置实例时直接抛出框架异常
    /// </summary>
    /// <remarks>
    /// 这是模块对宿主的硬性前置要求：必须先把 <c>IConfiguration</c> 放进服务集合。
    /// </remarks>
    [Fact]
    public void ConfigureServices_WhenConfigurationMissing_ThrowsXiHanException()
    {
        var context = new ServiceConfigurationContext(new ServiceCollection());

        Assert.Throws<XiHanException>(() => new XiHanKafkaEventBusModule().ConfigureServices(context));
    }

    /// <summary>
    /// 模块自身不注册任何事件总线实现
    /// </summary>
    /// <remarks>
    /// <see cref="KafkaDistributedEventBus"/> 依靠 <c>ExposeServices</c> 走约定注册；
    /// 若模块里又显式注册一次，容器里会出现两份实现，消费循环也会被启动两次。
    /// </remarks>
    [Fact]
    public void ConfigureServices_RegistersNoEventBusImplementation()
    {
        var context = CreateContext([]);

        new XiHanKafkaEventBusModule().ConfigureServices(context);

        Assert.DoesNotContain(context.Services, x => x.ServiceType == typeof(IDistributedEventBus));
        Assert.DoesNotContain(context.Services, x => x.ServiceType == typeof(KafkaDistributedEventBus));
    }

    /// <summary>
    /// 应用初始化阶段按具体类型索取事件总线，缺失时立即失败
    /// </summary>
    /// <remarks>
    /// 模块取的是具体类型而非 <c>IDistributedEventBus</c>，这要求 <c>ExposeServices</c> 里必须
    /// 同时暴露自身类型；否则应用会在启动阶段抛出而不是安静降级。
    /// </remarks>
    [Fact]
    public async Task OnApplicationInitializationAsync_WhenBusNotRegistered_Throws()
    {
        await using var provider = new ServiceCollection().BuildServiceProvider();
        var context = new ApplicationInitializationContext(provider);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new XiHanKafkaEventBusModule().OnApplicationInitializationAsync(context));
    }

    /// <summary>
    /// 构造带有指定配置源的服务配置上下文
    /// </summary>
    /// <param name="settings">配置键值</param>
    /// <returns>服务配置上下文</returns>
    private static ServiceConfigurationContext CreateContext(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();

        // 模块的 ConfigureServices 会读取配置，缺少 IConfiguration 单例实例会直接抛出
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

        return new ServiceConfigurationContext(services);
    }

    /// <summary>
    /// 从服务配置上下文解析 Kafka 选项
    /// </summary>
    /// <param name="context">服务配置上下文</param>
    /// <returns>Kafka 选项</returns>
    private static XiHanKafkaEventBusOptions ResolveOptions(ServiceConfigurationContext context)
    {
        using var provider = context.Services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<XiHanKafkaEventBusOptions>>().Value;
    }
}
