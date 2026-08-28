// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.EventBus.RabbitMQ.Tests;

/// <summary>
/// RabbitMQ 分布式事件总线模块测试
/// </summary>
/// <remarks>
/// 模块只做两件事：把配置节绑成选项，以及在应用初始化阶段拉起连接与消费者。
/// 前者一旦绑错节名，整套连接参数会静默退回默认值（连到 localhost 而不是生产 Broker）；
/// 后者依赖具体类型 <see cref="RabbitMQDistributedEventBus"/> 可从容器解析，这两条都在这里锁死。
/// </remarks>
public class XiHanRabbitMQEventBusModuleTests
{
    /// <summary>
    /// 模块继承框架模块基类，才能被模块加载器识别
    /// </summary>
    [Fact]
    public void Module_IsXiHanModule()
    {
        Assert.True(typeof(XiHanRabbitMQEventBusModule).IsAssignableTo(typeof(XiHanModule)));
        Assert.True(typeof(XiHanRabbitMQEventBusModule).IsAssignableTo(typeof(IXiHanModule)));
    }

    /// <summary>
    /// 模块仅依赖事件总线模块
    /// </summary>
    /// <remarks>
    /// 提供程序模块本身不注册事件总线基础设施，它只替换实现，
    /// 所以必须把 <c>XiHanEventBusModule</c> 拉进模块图，否则处理器登记与工作单元集成都不会发生。
    /// </remarks>
    [Fact]
    public void Module_DependsOnEventBusModule()
    {
        var attribute = typeof(XiHanRabbitMQEventBusModule).GetCustomAttribute<DependsOnAttribute>(false);

        Assert.NotNull(attribute);
        Assert.Equal(typeof(XiHanEventBusModule), Assert.Single(attribute.GetDependedTypes()));
    }

    /// <summary>
    /// 服务配置把选项注册进容器
    /// </summary>
    [Fact]
    public void ConfigureServices_RegistersOptionsConfiguration()
    {
        var context = CreateContext([]);

        new XiHanRabbitMQEventBusModule().ConfigureServices(context);

        Assert.Contains(
            context.Services,
            descriptor => descriptor.ServiceType == typeof(IConfigureOptions<XiHanRabbitMQEventBusOptions>));
    }

    /// <summary>
    /// 服务配置从约定配置节读取全部连接与拓扑参数
    /// </summary>
    [Fact]
    public void ConfigureServices_BindsOptionsFromSection()
    {
        var context = CreateContext(new Dictionary<string, string?>
        {
            [$"{XiHanRabbitMQEventBusOptions.SectionName}:Uri"] = "amqp://app:secret@mq.example.com:5673/prod",
            [$"{XiHanRabbitMQEventBusOptions.SectionName}:HostName"] = "mq.example.com",
            [$"{XiHanRabbitMQEventBusOptions.SectionName}:Port"] = "5673",
            [$"{XiHanRabbitMQEventBusOptions.SectionName}:UserName"] = "app",
            [$"{XiHanRabbitMQEventBusOptions.SectionName}:Password"] = "secret",
            [$"{XiHanRabbitMQEventBusOptions.SectionName}:VirtualHost"] = "/prod",
            [$"{XiHanRabbitMQEventBusOptions.SectionName}:ExchangeName"] = "Prod.Exchange",
            [$"{XiHanRabbitMQEventBusOptions.SectionName}:ExchangeType"] = "topic",
            [$"{XiHanRabbitMQEventBusOptions.SectionName}:QueueName"] = "Prod.Queue",
            [$"{XiHanRabbitMQEventBusOptions.SectionName}:PrefetchCount"] = "200",
            [$"{XiHanRabbitMQEventBusOptions.SectionName}:ClientProvidedName"] = "Prod.Client"
        });

        new XiHanRabbitMQEventBusModule().ConfigureServices(context);

        using var provider = context.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanRabbitMQEventBusOptions>>().Value;

        Assert.Equal("amqp://app:secret@mq.example.com:5673/prod", options.Uri);
        Assert.Equal("mq.example.com", options.HostName);
        Assert.Equal(5673, options.Port);
        Assert.Equal("app", options.UserName);
        Assert.Equal("secret", options.Password);
        Assert.Equal("/prod", options.VirtualHost);
        Assert.Equal("Prod.Exchange", options.ExchangeName);
        Assert.Equal("topic", options.ExchangeType);
        Assert.Equal("Prod.Queue", options.QueueName);
        Assert.Equal((ushort)200, options.PrefetchCount);
        Assert.Equal("Prod.Client", options.ClientProvidedName);
    }

    /// <summary>
    /// 配置节缺失时选项保持默认值，而不是被绑成空串
    /// </summary>
    [Fact]
    public void ConfigureServices_WithoutSection_KeepsDefaults()
    {
        var context = CreateContext(new Dictionary<string, string?>
        {
            ["Unrelated:Key"] = "value"
        });

        new XiHanRabbitMQEventBusModule().ConfigureServices(context);

        using var provider = context.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanRabbitMQEventBusOptions>>().Value;

        Assert.Null(options.Uri);
        Assert.Equal("localhost", options.HostName);
        Assert.Equal(5672, options.Port);
        Assert.Equal("Default", options.ExchangeName);
        Assert.Equal("Default.EventBus", options.QueueName);
        Assert.Equal((ushort)50, options.PrefetchCount);
    }

    /// <summary>
    /// 只配置部分键时，未配置的键仍保留默认值
    /// </summary>
    [Fact]
    public void ConfigureServices_WithPartialSection_OnlyOverridesConfiguredKeys()
    {
        var context = CreateContext(new Dictionary<string, string?>
        {
            [$"{XiHanRabbitMQEventBusOptions.SectionName}:HostName"] = "mq.internal"
        });

        new XiHanRabbitMQEventBusModule().ConfigureServices(context);

        using var provider = context.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanRabbitMQEventBusOptions>>().Value;

        Assert.Equal("mq.internal", options.HostName);
        Assert.Equal(5672, options.Port);
        Assert.Equal("guest", options.UserName);
        Assert.Equal("direct", options.ExchangeType);
    }

    /// <summary>
    /// 异步入口与同步入口行为一致
    /// </summary>
    [Fact]
    public async Task ConfigureServicesAsync_DelegatesToSyncOverload()
    {
        var context = CreateContext(new Dictionary<string, string?>
        {
            [$"{XiHanRabbitMQEventBusOptions.SectionName}:QueueName"] = "Async.Queue"
        });

        await new XiHanRabbitMQEventBusModule().ConfigureServicesAsync(context);

        using var provider = context.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanRabbitMQEventBusOptions>>().Value;

        Assert.Equal("Async.Queue", options.QueueName);
    }

    /// <summary>
    /// 应用初始化要求具体事件总线类型可从容器解析
    /// </summary>
    /// <remarks>
    /// 模块用 <c>GetRequiredService&lt;RabbitMQDistributedEventBus&gt;()</c> 取实例，
    /// 这条依赖来自实现类上的 <c>ExposeServices</c> 同时暴露了自身类型；
    /// 若哪天只暴露接口，启动会在这里失败而不是静默不消费，所以把「解析不到即抛出」当契约固定下来。
    /// </remarks>
    [Fact]
    public async Task OnApplicationInitializationAsync_WhenBusNotResolvable_Throws()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var context = new ApplicationInitializationContext(provider);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new XiHanRabbitMQEventBusModule().OnApplicationInitializationAsync(context));
    }

    /// <summary>
    /// 构造带有内存配置源的服务配置上下文
    /// </summary>
    /// <param name="settings">配置项</param>
    /// <returns>服务配置上下文</returns>
    private static ServiceConfigurationContext CreateContext(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();

        // 模块的 ConfigureServices 会读取配置，缺少 IConfiguration 单例实例会直接抛出
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

        return new ServiceConfigurationContext(services);
    }
}
