// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Core;
using XiHan.Framework.Bot.Telegram.Extensions.DependencyInjection;
using XiHan.Framework.Bot.Telegram.Handlers.Builtin;
using XiHan.Framework.Bot.Telegram.Messaging;
using XiHan.Framework.Bot.Telegram.MultiBot;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Routing;
using XiHan.Framework.Bot.Telegram.Stores;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.Extensions.DependencyInjection;

/// <summary>
/// <see cref="XiHanBotTelegramPlatformServiceCollectionExtensions"/> 多机器人平台注册扩展测试
/// </summary>
/// <remarks>
/// 平台的六个默认存储全部是 TryAdd 语义：生产环境必须能用数据库 / 分布式实现整条替换掉，
/// 否则多实例部署下幂等与会话状态会各算各的。
/// 路由与运行时组件必须是单例（目录持有路由表、注册表持有机器人实例、幂等器持有 TTL 字典），
/// 注册成瞬态会让这些状态每次注入都重置。宿主服务必须只注册一次，否则机器人会被拉起两遍。
/// </remarks>
public class XiHanBotTelegramPlatformServiceCollectionExtensionsTests
{
    /// <summary>
    /// 服务集合为空时抛参数空异常
    /// </summary>
    [Fact]
    public void AddXiHanBotTelegramPlatform_WhenServicesNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => XiHanBotTelegramPlatformServiceCollectionExtensions.AddXiHanBotTelegramPlatform(null!));
    }

    /// <summary>
    /// 返回原服务集合本身，支持链式调用
    /// </summary>
    [Fact]
    public void AddXiHanBotTelegramPlatform_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddXiHanBotTelegramPlatform());
    }

    /// <summary>
    /// 六个可替换服务均以单例注册默认实现
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="implementationType">默认实现类型</param>
    [Theory]
    [InlineData(typeof(ITelegramBotConfigStore), typeof(DefaultTelegramBotConfigStore))]
    [InlineData(typeof(ITelegramBotSettingsStore), typeof(DefaultTelegramBotSettingsStore))]
    [InlineData(typeof(ITelegramUpdateDeduplicator), typeof(InMemoryTelegramUpdateDeduplicator))]
    [InlineData(typeof(IConversationStateStore), typeof(InMemoryConversationStateStore))]
    [InlineData(typeof(ITelegramMessageAuditStore), typeof(NoOpTelegramMessageAuditStore))]
    [InlineData(typeof(ITelegramNotifier), typeof(TelegramNotifier))]
    public void AddXiHanBotTelegramPlatform_RegistersReplaceableServicesAsSingletons(
        Type serviceType,
        Type implementationType)
    {
        var services = new ServiceCollection();

        _ = services.AddXiHanBotTelegramPlatform();

        var descriptors = services.Where(x => x.ServiceType == serviceType).ToList();

        Assert.Single(descriptors);
        Assert.Equal(ServiceLifetime.Singleton, descriptors[0].Lifetime);
        Assert.Equal(implementationType, descriptors[0].ImplementationType);
    }

    /// <summary>
    /// 路由与运行时组件全部注册为单例
    /// </summary>
    [Fact]
    public void AddXiHanBotTelegramPlatform_RegistersRoutingAndRuntimeAsSingletons()
    {
        var services = new ServiceCollection();

        _ = services.AddXiHanBotTelegramPlatform();

        AssertSingleton(services, typeof(TelegramBotHandlerCatalog));
        AssertSingleton(services, typeof(TelegramCommandRouter));
        AssertSingleton(services, typeof(TelegramCallbackRouter));
        AssertSingleton(services, typeof(TelegramReplyRouter));
        AssertSingleton(services, typeof(TelegramMessageRouter));
        AssertSingleton(services, typeof(TelegramInlineQueryRouter));
        AssertSingleton(services, typeof(TelegramUpdateDispatcher));
        AssertSingleton(services, typeof(BotRegistry));
        AssertSingleton(services, typeof(TelegramBotManager));
    }

    /// <summary>
    /// 宿主服务注册一次；重复调用注册扩展也只注册一次
    /// </summary>
    [Fact]
    public void AddXiHanBotTelegramPlatform_RegistersHostedServiceExactlyOnce()
    {
        var services = new ServiceCollection();

        _ = services.AddXiHanBotTelegramPlatform();
        _ = services.AddXiHanBotTelegramPlatform();

        var descriptors = services.Where(x => x.ServiceType == typeof(IHostedService)).ToList();

        Assert.Single(descriptors);
        Assert.Equal(typeof(TelegramBotHostedService), descriptors[0].ImplementationType);
    }

    /// <summary>
    /// 全部平台服务都能从真实容器解析出来，且单例复用同一实例
    /// </summary>
    [Fact]
    public void AddXiHanBotTelegramPlatform_ResolvesAllServices()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddXiHanBotTelegramPlatform();
        using var provider = services.BuildServiceProvider();

        Assert.IsType<DefaultTelegramBotConfigStore>(provider.GetRequiredService<ITelegramBotConfigStore>());
        Assert.IsType<DefaultTelegramBotSettingsStore>(provider.GetRequiredService<ITelegramBotSettingsStore>());
        Assert.IsType<InMemoryTelegramUpdateDeduplicator>(provider.GetRequiredService<ITelegramUpdateDeduplicator>());
        Assert.IsType<InMemoryConversationStateStore>(provider.GetRequiredService<IConversationStateStore>());
        Assert.IsType<NoOpTelegramMessageAuditStore>(provider.GetRequiredService<ITelegramMessageAuditStore>());
        Assert.IsType<TelegramNotifier>(provider.GetRequiredService<ITelegramNotifier>());

        var manager = provider.GetRequiredService<TelegramBotManager>();
        Assert.Same(manager, provider.GetRequiredService<TelegramBotManager>());
        Assert.Same(provider.GetRequiredService<BotRegistry>(), provider.GetRequiredService<BotRegistry>());
        Assert.NotNull(provider.GetRequiredService<TelegramUpdateDispatcher>());
        Assert.NotNull(provider.GetRequiredService<TelegramBotHandlerCatalog>());
    }

    /// <summary>
    /// 默认注册出来的平台是关闭的：没有启用、没有机器人、没有处理器
    /// </summary>
    [Fact]
    public void AddXiHanBotTelegramPlatform_DefaultWiringIsDisabledAndEmpty()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddXiHanBotTelegramPlatform();
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<TelegramBotPlatformOptions>>().Value;
        var catalog = provider.GetRequiredService<TelegramBotHandlerCatalog>();
        var manager = provider.GetRequiredService<TelegramBotManager>();

        Assert.False(options.Settings.Enabled);
        Assert.Empty(options.Bots);
        Assert.Empty(catalog.CommandRoutes);
        Assert.Empty(catalog.CallbackRoutes);
        Assert.False(manager.IsStarted);
        Assert.Equal(0, provider.GetRequiredService<BotRegistry>().Count);
    }

    /// <summary>
    /// 配置委托被写入平台选项
    /// </summary>
    [Fact]
    public void AddXiHanBotTelegramPlatform_AppliesConfigureDelegate()
    {
        var services = new ServiceCollection();

        _ = services.AddXiHanBotTelegramPlatform(platform =>
        {
            platform.Settings.Enabled = true;
            platform.Settings.WebhookSecretToken = "s3cr3t";
            platform.Retry.MaxRetries = 5;
            platform.Texts.HelpHeader = "指令：";
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<TelegramBotPlatformOptions>>().Value;

        Assert.True(options.Settings.Enabled);
        Assert.Equal("s3cr3t", options.Settings.WebhookSecretToken);
        Assert.Equal(5, options.Retry.MaxRetries);
        Assert.Equal("指令：", options.Texts.HelpHeader);
    }

    /// <summary>
    /// 应用层预先注册的存储不被默认实现覆盖（TryAdd 语义）
    /// </summary>
    [Fact]
    public void AddXiHanBotTelegramPlatform_KeepsPreRegisteredStores()
    {
        var settingsStore = new FakeTelegramBotSettingsStore();
        var configStore = new FakeTelegramBotConfigStore();
        var deduplicator = new FakeTelegramUpdateDeduplicator();
        var stateStore = new FakeConversationStateStore();

        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton<ITelegramBotSettingsStore>(settingsStore);
        _ = services.AddSingleton<ITelegramBotConfigStore>(configStore);
        _ = services.AddSingleton<ITelegramUpdateDeduplicator>(deduplicator);
        _ = services.AddSingleton<IConversationStateStore>(stateStore);

        _ = services.AddXiHanBotTelegramPlatform();

        using var provider = services.BuildServiceProvider();
        Assert.Same(settingsStore, provider.GetRequiredService<ITelegramBotSettingsStore>());
        Assert.Same(configStore, provider.GetRequiredService<ITelegramBotConfigStore>());
        Assert.Same(deduplicator, provider.GetRequiredService<ITelegramUpdateDeduplicator>());
        Assert.Same(stateStore, provider.GetRequiredService<IConversationStateStore>());
    }

    /// <summary>
    /// 注册处理器时服务集合为空则抛参数空异常
    /// </summary>
    [Fact]
    public void AddTelegramBotHandler_WhenServicesNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => XiHanBotTelegramPlatformServiceCollectionExtensions.AddTelegramBotHandler<TestOrderCommandHandler>(null!));
    }

    /// <summary>
    /// 注册处理器同时写入 DI（瞬态）与路由登记本
    /// </summary>
    [Fact]
    public void AddTelegramBotHandler_RegistersTransientAndAddsToCatalogOptions()
    {
        var services = new ServiceCollection();

        _ = services.AddTelegramBotHandler<TestOrderCommandHandler>();

        var descriptors = services.Where(x => x.ServiceType == typeof(TestOrderCommandHandler)).ToList();
        Assert.Single(descriptors);
        Assert.Equal(ServiceLifetime.Transient, descriptors[0].Lifetime);

        using var provider = services.BuildServiceProvider();
        var handlerOptions = provider.GetRequiredService<IOptions<TelegramBotHandlerOptions>>().Value;
        Assert.Single(handlerOptions.Handlers);
        Assert.Equal(typeof(TestOrderCommandHandler), handlerOptions.Handlers[0]);
    }

    /// <summary>
    /// 重复注册同一处理器只登记一次，避免目录构建时误报「命令重复」
    /// </summary>
    [Fact]
    public void AddTelegramBotHandler_CalledTwice_RegistersHandlerOnce()
    {
        var services = new ServiceCollection();

        _ = services.AddTelegramBotHandler<TestOrderCommandHandler>();
        _ = services.AddTelegramBotHandler<TestOrderCommandHandler>();

        Assert.Equal(1, services.Count(x => x.ServiceType == typeof(TestOrderCommandHandler)));

        using var provider = services.BuildServiceProvider();
        Assert.Single(provider.GetRequiredService<IOptions<TelegramBotHandlerOptions>>().Value.Handlers);
    }

    /// <summary>
    /// 返回原服务集合本身，支持链式调用
    /// </summary>
    [Fact]
    public void AddTelegramBotHandler_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddTelegramBotHandler<TestOrderCommandHandler>());
    }

    /// <summary>
    /// 注册内置处理器时服务集合为空则抛参数空异常
    /// </summary>
    [Fact]
    public void AddTelegramBotBuiltinHandlers_WhenServicesNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => XiHanBotTelegramPlatformServiceCollectionExtensions.AddTelegramBotBuiltinHandlers(null!));
    }

    /// <summary>
    /// 内置处理器注册 /start、/help、/myid 三个命令处理器
    /// </summary>
    [Fact]
    public void AddTelegramBotBuiltinHandlers_RegistersThreeBuiltinCommands()
    {
        var services = new ServiceCollection();

        _ = services.AddTelegramBotBuiltinHandlers();

        using var provider = services.BuildServiceProvider();
        var handlers = provider.GetRequiredService<IOptions<TelegramBotHandlerOptions>>().Value.Handlers;

        Assert.Equal(3, handlers.Count);
        Assert.Contains(typeof(StartCommandHandler), handlers);
        Assert.Contains(typeof(HelpCommandHandler), handlers);
        Assert.Contains(typeof(MyIdCommandHandler), handlers);
    }

    /// <summary>
    /// 内置处理器登记后目录能建出 /start、/help、/h、/myid、/id 五条命令路由
    /// </summary>
    [Fact]
    public void AddTelegramBotBuiltinHandlers_BuildsBuiltinCommandRoutes()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddXiHanBotTelegramPlatform();
        _ = services.AddTelegramBotBuiltinHandlers();

        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<TelegramBotHandlerCatalog>();

        Assert.Equal(5, catalog.CommandRoutes.Count);
        Assert.True(catalog.CommandRoutes.ContainsKey("/start"));
        Assert.True(catalog.CommandRoutes.ContainsKey("/help"));
        Assert.True(catalog.CommandRoutes.ContainsKey("/h"));
        Assert.True(catalog.CommandRoutes.ContainsKey("/myid"));
        Assert.True(catalog.CommandRoutes.ContainsKey("/id"));
        Assert.Equal(3, catalog.GetVisibleCommands().Count);
    }

    /// <summary>
    /// 内置处理器可从容器解析（构造依赖齐备）
    /// </summary>
    [Fact]
    public void AddTelegramBotBuiltinHandlers_ResolvesHandlersFromContainer()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddXiHanBotTelegramPlatform();
        _ = services.AddTelegramBotBuiltinHandlers();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<StartCommandHandler>());
        Assert.NotNull(provider.GetRequiredService<HelpCommandHandler>());
        Assert.NotNull(provider.GetRequiredService<MyIdCommandHandler>());
    }

    /// <summary>
    /// 返回原服务集合本身，支持链式调用
    /// </summary>
    [Fact]
    public void AddTelegramBotBuiltinHandlers_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddTelegramBotBuiltinHandlers());
    }

    /// <summary>
    /// 断言指定服务类型注册为单例且只有一条注册
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="serviceType">服务类型</param>
    private static void AssertSingleton(IServiceCollection services, Type serviceType)
    {
        var descriptors = services.Where(x => x.ServiceType == serviceType).ToList();

        Assert.Single(descriptors);
        Assert.Equal(ServiceLifetime.Singleton, descriptors[0].Lifetime);
        Assert.Equal(serviceType, descriptors[0].ImplementationType);
    }
}
