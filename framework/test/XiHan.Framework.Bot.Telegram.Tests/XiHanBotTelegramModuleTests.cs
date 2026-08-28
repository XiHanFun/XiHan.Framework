// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Messaging;
using XiHan.Framework.Bot.Telegram.MultiBot;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Routing;
using XiHan.Framework.Bot.Telegram.Stores;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Bot.Telegram.Tests;

/// <summary>
/// <see cref="XiHanBotTelegramModule"/> Telegram 模块装配测试
/// </summary>
/// <remarks>
/// 模块本身没有业务逻辑，契约只有三条：声明对 Bot 主模块的依赖、绑定平台配置节、
/// 同时装配「单发通道」与「多机器人平台」两套服务。
/// 最关键的一条是装配完成后平台仍然是关闭的——引入模块不等于自动上线机器人。
/// </remarks>
public class XiHanBotTelegramModuleTests
{
    /// <summary>
    /// 模块继承框架模块基类，可被模块加载器识别
    /// </summary>
    [Fact]
    public void Module_InheritsXiHanModule()
    {
        Assert.IsAssignableFrom<XiHanModule>(new XiHanBotTelegramModule());
    }

    /// <summary>
    /// 模块声明依赖 Bot 主模块，保证 Bot 内核先于 Telegram 提供者装配
    /// </summary>
    [Fact]
    public void Module_DependsOnBotModule()
    {
        var attributes = typeof(XiHanBotTelegramModule).GetCustomAttributes<DependsOnAttribute>().ToList();

        Assert.Single(attributes);
        Assert.Contains(typeof(XiHanBotModule), attributes[0].GetDependedTypes());
    }

    /// <summary>
    /// 装配单发通道：Telegram 提供者与默认配置存储
    /// </summary>
    [Fact]
    public void ConfigureServices_RegistersSingleChannelServices()
    {
        using var provider = ConfigureAndBuild();

        var providers = provider.GetServices<IBotProvider>().ToList();

        Assert.IsType<DefaultTelegramConfigStore>(provider.GetRequiredService<ITelegramConfigStore>());
        Assert.Single(providers);
        Assert.IsType<TelegramBotProvider>(providers[0]);
    }

    /// <summary>
    /// 装配多机器人平台：存储、路由、分发器、发送门面与运行时
    /// </summary>
    [Fact]
    public void ConfigureServices_RegistersMultiBotPlatformServices()
    {
        using var provider = ConfigureAndBuild();

        Assert.IsType<DefaultTelegramBotConfigStore>(provider.GetRequiredService<ITelegramBotConfigStore>());
        Assert.IsType<DefaultTelegramBotSettingsStore>(provider.GetRequiredService<ITelegramBotSettingsStore>());
        Assert.IsType<InMemoryTelegramUpdateDeduplicator>(provider.GetRequiredService<ITelegramUpdateDeduplicator>());
        Assert.IsType<InMemoryConversationStateStore>(provider.GetRequiredService<IConversationStateStore>());
        Assert.IsType<NoOpTelegramMessageAuditStore>(provider.GetRequiredService<ITelegramMessageAuditStore>());
        Assert.IsType<TelegramNotifier>(provider.GetRequiredService<ITelegramNotifier>());
        Assert.NotNull(provider.GetRequiredService<TelegramBotHandlerCatalog>());
        Assert.NotNull(provider.GetRequiredService<TelegramBotManager>());
        Assert.NotNull(provider.GetRequiredService<BotRegistry>());
    }

    /// <summary>
    /// 装配完成后平台仍然处于关闭状态，且没有登记任何处理器
    /// </summary>
    [Fact]
    public void ConfigureServices_LeavesPlatformDisabledAndWithoutHandlers()
    {
        using var provider = ConfigureAndBuild();

        var options = provider.GetRequiredService<IOptions<TelegramBotPlatformOptions>>().Value;
        var catalog = provider.GetRequiredService<TelegramBotHandlerCatalog>();

        Assert.False(options.Settings.Enabled);
        Assert.Empty(options.Bots);
        Assert.Empty(catalog.CommandRoutes);
        Assert.Empty(catalog.CallbackRoutes);
        Assert.False(provider.GetRequiredService<TelegramBotManager>().IsStarted);
    }

    /// <summary>
    /// 平台配置节绑定生效：配置文件里的设置能被读出来
    /// </summary>
    /// <remarks>
    /// 配置节名一旦对不上，存量 appsettings 里的 Telegram 段会被静默忽略，
    /// 表现为「配置改了没反应」，很难排查，因此这里做一次真实绑定验证。
    /// </remarks>
    [Fact]
    public void ConfigureServices_BindsPlatformConfigurationSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{TelegramBotPlatformOptions.SectionName}:Settings:Enabled"] = "true",
                [$"{TelegramBotPlatformOptions.SectionName}:Settings:WebhookSecretToken"] = "s3cr3t",
                [$"{TelegramBotPlatformOptions.SectionName}:Settings:ManagerRefreshSeconds"] = "17",
                [$"{TelegramBotPlatformOptions.SectionName}:Retry:MaxRetries"] = "9",
                [$"{TelegramBotPlatformOptions.SectionName}:Texts:HelpHeader"] = "指令：",
                [$"{TelegramBotPlatformOptions.SectionName}:Bots:0:Name"] = "main-bot",
                [$"{TelegramBotPlatformOptions.SectionName}:Bots:0:Token"] = "123456:AAHfake-telegram-token"
            })
            .Build();

        using var provider = ConfigureAndBuild(configuration);
        var options = provider.GetRequiredService<IOptions<TelegramBotPlatformOptions>>().Value;

        Assert.True(options.Settings.Enabled);
        Assert.Equal("s3cr3t", options.Settings.WebhookSecretToken);
        Assert.Equal(17, options.Settings.ManagerRefreshSeconds);
        Assert.Equal(9, options.Retry.MaxRetries);
        Assert.Equal("指令：", options.Texts.HelpHeader);
        Assert.Single(options.Bots);
        Assert.Equal("main-bot", options.Bots[0].Name);
    }

    /// <summary>
    /// 平台配置节缺省时使用默认值，不会因为绑定失败而报错
    /// </summary>
    [Fact]
    public void ConfigureServices_WhenSectionMissing_UsesDefaults()
    {
        using var provider = ConfigureAndBuild();
        var options = provider.GetRequiredService<IOptions<TelegramBotPlatformOptions>>().Value;

        Assert.False(options.Settings.Enabled);
        Assert.Equal(5, options.Settings.ManagerRefreshSeconds);
        Assert.Equal(3, options.Retry.MaxRetries);
        Assert.Equal(TelegramBotPlatformConsts.DefaultWebhookRoutePrefix, options.Settings.WebhookRoutePrefix);
    }

    /// <summary>
    /// 重复执行 ConfigureServices 不会重复注册提供者与宿主服务
    /// </summary>
    [Fact]
    public void ConfigureServices_CalledTwice_DoesNotDuplicateRegistrations()
    {
        var services = CreateServices();
        var context = new ServiceConfigurationContext(services);
        var module = new XiHanBotTelegramModule();

        module.ConfigureServices(context);
        module.ConfigureServices(context);

        using var provider = services.BuildServiceProvider();

        Assert.Single(provider.GetServices<IBotProvider>());
        Assert.Single(provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>());
    }

    /// <summary>
    /// 构造带日志与配置的服务集合
    /// </summary>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    private static ServiceCollection CreateServices(IConfiguration? configuration = null)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton<IConfiguration>(configuration ?? new ConfigurationBuilder().Build());
        return services;
    }

    /// <summary>
    /// 执行模块装配并构建容器
    /// </summary>
    /// <param name="configuration">配置</param>
    /// <returns>服务提供者</returns>
    private static ServiceProvider ConfigureAndBuild(IConfiguration? configuration = null)
    {
        var services = CreateServices(configuration);

        new XiHanBotTelegramModule().ConfigureServices(new ServiceConfigurationContext(services));

        return services.BuildServiceProvider();
    }
}
