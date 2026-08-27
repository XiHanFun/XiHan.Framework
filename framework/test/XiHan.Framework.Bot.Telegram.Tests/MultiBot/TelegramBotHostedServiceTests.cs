// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Extensions.DependencyInjection;
using XiHan.Framework.Bot.Telegram.MultiBot;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.MultiBot;

/// <summary>
/// <see cref="TelegramBotHostedService"/> 宿主服务测试
/// </summary>
/// <remarks>
/// 宿主服务只做一件事：把管理器的生命周期挂到应用生命周期上，并且<b>吞掉启动异常</b>。
/// 后一点是刻意设计——Telegram 起不来不该让整个应用起不来。
/// 这里用「平台未启用」的管理器（启动过程完全不触网）验证启停联动。
/// </remarks>
public class TelegramBotHostedServiceTests
{
    /// <summary>
    /// 管理器为空时抛参数空异常
    /// </summary>
    [Fact]
    public void Constructor_WhenManagerNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TelegramBotHostedService(null!, NullLogger<TelegramBotHostedService>.Instance));
    }

    /// <summary>
    /// 日志记录器为空时抛参数空异常
    /// </summary>
    [Fact]
    public void Constructor_WhenLoggerNull_Throws()
    {
        using var harness = CreateHarness();

        Assert.Throws<ArgumentNullException>(() => new TelegramBotHostedService(harness.Manager, null!));
    }

    /// <summary>
    /// 实现 IHostedService，可被宿主统一编排
    /// </summary>
    [Fact]
    public void Type_ImplementsHostedServiceAbstraction()
    {
        using var harness = CreateHarness();
        var hostedService = new TelegramBotHostedService(harness.Manager, NullLogger<TelegramBotHostedService>.Instance);

        Assert.IsAssignableFrom<IHostedService>(hostedService);
    }

    /// <summary>
    /// 启动时拉起管理器
    /// </summary>
    [Fact]
    public async Task StartAsync_StartsManager()
    {
        using var harness = CreateHarness();
        var hostedService = new TelegramBotHostedService(harness.Manager, NullLogger<TelegramBotHostedService>.Instance);

        await hostedService.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(harness.Manager.IsStarted);
    }

    /// <summary>
    /// 停止时关停管理器
    /// </summary>
    [Fact]
    public async Task StopAsync_StopsManager()
    {
        using var harness = CreateHarness();
        var hostedService = new TelegramBotHostedService(harness.Manager, NullLogger<TelegramBotHostedService>.Instance);
        await hostedService.StartAsync(TestContext.Current.CancellationToken);

        await hostedService.StopAsync(TestContext.Current.CancellationToken);

        Assert.False(harness.Manager.IsStarted);
    }

    /// <summary>
    /// 管理器初始化失败不会向宿主抛异常，应用照常启动
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenManagerInitializationFails_DoesNotThrow()
    {
        using var harness = CreateHarness();
        harness.SettingsStore.ExceptionToThrow = new InvalidOperationException("设置存储不可用");
        var hostedService = new TelegramBotHostedService(harness.Manager, NullLogger<TelegramBotHostedService>.Instance);

        await hostedService.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(harness.Manager.IsStarted);

        await hostedService.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 未启动就停止是空操作，同样不抛异常
    /// </summary>
    [Fact]
    public async Task StopAsync_WhenNeverStarted_DoesNotThrow()
    {
        using var harness = CreateHarness();
        var hostedService = new TelegramBotHostedService(harness.Manager, NullLogger<TelegramBotHostedService>.Instance);

        await hostedService.StopAsync(TestContext.Current.CancellationToken);

        Assert.False(harness.Manager.IsStarted);
    }

    /// <summary>
    /// 构造宿主服务测试装置
    /// </summary>
    /// <returns>测试装置</returns>
    private static HostedServiceHarness CreateHarness()
    {
        var settingsStore = new FakeTelegramBotSettingsStore
        {
            Settings = new TelegramBotSettings { ManagerRefreshSeconds = 0 }
        };

        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton<ITelegramBotSettingsStore>(settingsStore);
        _ = services.AddSingleton<ITelegramBotConfigStore>(new FakeTelegramBotConfigStore());
        _ = services.AddXiHanBotTelegramPlatform();

        var provider = services.BuildServiceProvider();

        return new HostedServiceHarness(provider.GetRequiredService<TelegramBotManager>(), settingsStore, provider);
    }

    /// <summary>
    /// 宿主服务测试装置
    /// </summary>
    private sealed class HostedServiceHarness : IDisposable
    {
        private readonly ServiceProvider _provider;

        public HostedServiceHarness(
            TelegramBotManager manager,
            FakeTelegramBotSettingsStore settingsStore,
            ServiceProvider provider)
        {
            Manager = manager;
            SettingsStore = settingsStore;
            _provider = provider;
        }

        public TelegramBotManager Manager { get; }

        public FakeTelegramBotSettingsStore SettingsStore { get; }

        public void Dispose()
        {
            _provider.Dispose();
        }
    }
}
