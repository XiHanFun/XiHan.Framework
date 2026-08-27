// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Extensions.DependencyInjection;
using XiHan.Framework.Bot.Telegram.MultiBot;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.MultiBot;

/// <summary>
/// <see cref="TelegramBotManager"/> 多机器人管理器测试
/// </summary>
/// <remarks>
/// 真正拉起机器人必然要连 Telegram（SetWebhook / DeleteWebhook / GetMe），因此这里只覆盖
/// 「不会触发任何 Bot API 调用」的两类路径：
/// 1）平台未启用（Enabled = false）——管理器空转，连配置列表都不会去读；
/// 2）平台已启用但配置列表为空——读了配置但没有机器人可拉起。
/// 这两条恰好也是最容易被改坏的安全基线（默认不上线、配置为空不乱跑）。
/// 另外覆盖设置归一化（Webhook 前缀/密钥）、启停幂等与入队守卫。
/// </remarks>
public class TelegramBotManagerTests
{
    /// <summary>
    /// 任一构造依赖为空时抛参数空异常
    /// </summary>
    [Fact]
    public void Constructor_WhenDependencyNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new TelegramBotManager(null!, null!, null!, null!, null!));
        Assert.Throws<ArgumentNullException>(() => new TelegramBotManager(new BotRegistry(), null!, null!, null!, null!));
    }

    /// <summary>
    /// 未启动时状态为「未启动 + 未启用 + 长轮询 + 零机器人」
    /// </summary>
    [Fact]
    public void GetStatus_BeforeStart_ReportsIdlePollingState()
    {
        using var harness = CreateHarness();

        var status = harness.Manager.GetStatus();

        Assert.False(harness.Manager.IsStarted);
        Assert.False(harness.Manager.IsEnabled);
        Assert.False(status.IsStarted);
        Assert.False(status.Enabled);
        Assert.Equal("polling", status.TransportMode);
        Assert.Equal(0, status.TotalBots);
        Assert.Empty(status.Bots);
    }

    /// <summary>
    /// 未启动时 Webhook 前缀取平台默认值，密钥为空
    /// </summary>
    [Fact]
    public void WebhookSettings_BeforeStart_AreDefaults()
    {
        using var harness = CreateHarness();

        Assert.Equal(TelegramBotPlatformConsts.DefaultWebhookRoutePrefix, harness.Manager.WebhookRoutePrefix);
        Assert.Equal(string.Empty, harness.Manager.WebhookSecretToken);
    }

    /// <summary>
    /// 平台未启用时启动为空转：读了设置但不去读机器人配置，也不拉起任何机器人
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenPlatformDisabled_RunsIdleWithoutLoadingConfigs()
    {
        using var harness = CreateHarness(configs: [TelegramTestFactory.CreateConfig(name: "main-bot")]);

        await harness.Manager.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(harness.Manager.IsStarted);
        Assert.False(harness.Manager.IsEnabled);
        Assert.Equal(0, harness.ConfigStore.GetCount);
        Assert.Equal(0, harness.Registry.Count);
        Assert.Equal(0, harness.Manager.GetStatus().TotalBots);
    }

    /// <summary>
    /// 平台启用但没有机器人配置时读取配置列表后空转
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenEnabledWithoutConfigs_LoadsConfigsAndStaysEmpty()
    {
        using var harness = CreateHarness(settings => settings.Enabled = true);

        await harness.Manager.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(harness.Manager.IsStarted);
        Assert.True(harness.Manager.IsEnabled);
        Assert.Equal(1, harness.ConfigStore.GetCount);
        Assert.Equal(0, harness.Registry.Count);
        Assert.Equal(0, harness.Manager.GetStatus().TotalBots);
    }

    /// <summary>
    /// 启动时归一化 Webhook 路由前缀（补前导斜杠、去尾部斜杠）并裁剪密钥空白
    /// </summary>
    [Fact]
    public async Task StartAsync_NormalizesWebhookRoutePrefixAndTrimsSecret()
    {
        using var harness = CreateHarness(settings =>
        {
            settings.WebhookBaseUrl = "https://example.com/";
            settings.WebhookRoutePrefix = "api/tg/hook/";
            settings.WebhookSecretToken = "  s3cr3t  ";
        });

        await harness.Manager.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal("/api/tg/hook", harness.Manager.WebhookRoutePrefix);
        Assert.Equal("s3cr3t", harness.Manager.WebhookSecretToken);
        Assert.Equal("webhook", harness.Manager.GetStatus().TransportMode);
    }

    /// <summary>
    /// Webhook 路由前缀为空白时回落到平台默认前缀
    /// </summary>
    /// <param name="routePrefix">配置的路由前缀</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StartAsync_WhenRoutePrefixBlank_FallsBackToDefault(string routePrefix)
    {
        using var harness = CreateHarness(settings => settings.WebhookRoutePrefix = routePrefix);

        await harness.Manager.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal(TelegramBotPlatformConsts.DefaultWebhookRoutePrefix, harness.Manager.WebhookRoutePrefix);
    }

    /// <summary>
    /// 未配置 WebhookBaseUrl 时传输模式为长轮询
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenWebhookBaseUrlEmpty_UsesPollingTransport()
    {
        using var harness = CreateHarness();

        await harness.Manager.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal("polling", harness.Manager.GetStatus().TransportMode);
    }

    /// <summary>
    /// 重复启动是幂等的，不会重复读取设置
    /// </summary>
    [Fact]
    public async Task StartAsync_CalledTwice_IsIdempotent()
    {
        using var harness = CreateHarness();

        await harness.Manager.StartAsync(TestContext.Current.CancellationToken);
        var settingsReadsAfterFirstStart = harness.SettingsStore.GetCount;
        await harness.Manager.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(harness.Manager.IsStarted);
        Assert.Equal(settingsReadsAfterFirstStart, harness.SettingsStore.GetCount);
    }

    /// <summary>
    /// 停止后状态回到未启动
    /// </summary>
    [Fact]
    public async Task StopAsync_AfterStart_MarksManagerStopped()
    {
        using var harness = CreateHarness();
        await harness.Manager.StartAsync(TestContext.Current.CancellationToken);

        await harness.Manager.StopAsync(TestContext.Current.CancellationToken);

        Assert.False(harness.Manager.IsStarted);
        Assert.False(harness.Manager.GetStatus().IsStarted);
    }

    /// <summary>
    /// 未启动时停止是空操作，不会去读设置
    /// </summary>
    [Fact]
    public async Task StopAsync_WhenNotStarted_IsNoOp()
    {
        using var harness = CreateHarness();

        await harness.Manager.StopAsync(TestContext.Current.CancellationToken);

        Assert.False(harness.Manager.IsStarted);
        Assert.Equal(0, harness.SettingsStore.GetCount);
    }

    /// <summary>
    /// 停止后可以再次启动
    /// </summary>
    [Fact]
    public async Task StartAsync_AfterStop_CanStartAgain()
    {
        using var harness = CreateHarness();

        await harness.Manager.StartAsync(TestContext.Current.CancellationToken);
        await harness.Manager.StopAsync(TestContext.Current.CancellationToken);
        await harness.Manager.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(harness.Manager.IsStarted);
    }

    /// <summary>
    /// 管理器未启动时 RefreshNow 等价于启动
    /// </summary>
    [Fact]
    public async Task RefreshNowAsync_WhenNotStarted_StartsManager()
    {
        using var harness = CreateHarness();

        await harness.Manager.RefreshNowAsync(TestContext.Current.CancellationToken);

        Assert.True(harness.Manager.IsStarted);
    }

    /// <summary>
    /// 已启动时 RefreshNow 重新读取设置，配置改动无需重启即可生效
    /// </summary>
    [Fact]
    public async Task RefreshNowAsync_WhenStarted_RereadsSettings()
    {
        using var harness = CreateHarness();
        await harness.Manager.StartAsync(TestContext.Current.CancellationToken);
        Assert.False(harness.Manager.IsEnabled);

        harness.SettingsStore.Settings = new TelegramBotSettings { Enabled = true, ManagerRefreshSeconds = 0 };
        await harness.Manager.RefreshNowAsync(TestContext.Current.CancellationToken);

        Assert.True(harness.Manager.IsEnabled);
        Assert.Equal(1, harness.ConfigStore.GetCount);
    }

    /// <summary>
    /// 平台从启用切回停用时，刷新会把管理器带回空转状态
    /// </summary>
    [Fact]
    public async Task RefreshNowAsync_WhenPlatformTurnedOff_StopsReportingEnabled()
    {
        using var harness = CreateHarness(settings => settings.Enabled = true);
        await harness.Manager.StartAsync(TestContext.Current.CancellationToken);
        Assert.True(harness.Manager.IsEnabled);

        harness.SettingsStore.Settings = new TelegramBotSettings { Enabled = false, ManagerRefreshSeconds = 0 };
        await harness.Manager.RefreshNowAsync(TestContext.Current.CancellationToken);

        Assert.False(harness.Manager.IsEnabled);
        Assert.Equal(0, harness.Manager.GetStatus().TotalBots);
    }

    /// <summary>
    /// 设置存储不可用时启动不被阻断，由后续刷新周期重试
    /// </summary>
    /// <remarks>
    /// 宿主启动阶段数据库还没就绪是常态，管理器必须能带病启动，
    /// 否则整个应用会因为「Telegram 设置读不到」而起不来。
    /// </remarks>
    [Fact]
    public async Task StartAsync_WhenSettingsStoreThrows_StillMarksStarted()
    {
        using var harness = CreateHarness();
        harness.SettingsStore.ExceptionToThrow = new InvalidOperationException("设置存储不可用");

        await harness.Manager.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(harness.Manager.IsStarted);
        Assert.False(harness.Manager.IsEnabled);

        await harness.Manager.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 管理器未运行时入队被拒绝，不会把 Update 交给分发管线
    /// </summary>
    /// <remarks>
    /// Webhook 中间件在管理器还没起来（或已关停）时仍可能收到请求，
    /// 这时必须直接丢弃：占了幂等标记又没真正处理，会让 Telegram 的重发也一并被吃掉。
    /// </remarks>
    [Fact]
    public void QueueDispatch_WhenManagerNotStarted_DropsUpdate()
    {
        using var harness = CreateHarness();
        using var bot = TelegramTestFactory.CreateBot();

        harness.Manager.QueueDispatch(bot, TelegramTestFactory.CreateMessageUpdate());

        Assert.Empty(harness.Deduplicator.Marked);
    }

    /// <summary>
    /// 停止之后入队同样被拒绝
    /// </summary>
    [Fact]
    public async Task QueueDispatch_AfterStop_DropsUpdate()
    {
        using var harness = CreateHarness();
        using var bot = TelegramTestFactory.CreateBot();
        await harness.Manager.StartAsync(TestContext.Current.CancellationToken);
        await harness.Manager.StopAsync(TestContext.Current.CancellationToken);

        harness.Manager.QueueDispatch(bot, TelegramTestFactory.CreateMessageUpdate());

        Assert.Empty(harness.Deduplicator.Marked);
    }

    /// <summary>
    /// 构造管理器测试装置
    /// </summary>
    /// <param name="configureSettings">平台设置配置委托</param>
    /// <param name="configs">机器人配置列表</param>
    /// <returns>测试装置</returns>
    private static ManagerHarness CreateHarness(
        Action<TelegramBotSettings>? configureSettings = null,
        TelegramBotConfig[]? configs = null)
    {
        // 刷新周期设为 0：关掉后台刷新循环，让每条用例的时序完全可控
        var settingsStore = new FakeTelegramBotSettingsStore
        {
            Settings = new TelegramBotSettings { ManagerRefreshSeconds = 0 }
        };
        configureSettings?.Invoke(settingsStore.Settings);

        var configStore = new FakeTelegramBotConfigStore();
        if (configs is not null)
        {
            configStore.Configs = [.. configs];
        }

        var deduplicator = new FakeTelegramUpdateDeduplicator();

        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton<ITelegramBotSettingsStore>(settingsStore);
        _ = services.AddSingleton<ITelegramBotConfigStore>(configStore);
        _ = services.AddSingleton<ITelegramUpdateDeduplicator>(deduplicator);
        _ = services.AddXiHanBotTelegramPlatform();

        var provider = services.BuildServiceProvider();

        return new ManagerHarness(
            provider.GetRequiredService<TelegramBotManager>(),
            provider.GetRequiredService<BotRegistry>(),
            settingsStore,
            configStore,
            deduplicator,
            provider);
    }

    /// <summary>
    /// 管理器测试装置
    /// </summary>
    private sealed class ManagerHarness : IDisposable
    {
        private readonly ServiceProvider _provider;

        public ManagerHarness(
            TelegramBotManager manager,
            BotRegistry registry,
            FakeTelegramBotSettingsStore settingsStore,
            FakeTelegramBotConfigStore configStore,
            FakeTelegramUpdateDeduplicator deduplicator,
            ServiceProvider provider)
        {
            Manager = manager;
            Registry = registry;
            SettingsStore = settingsStore;
            ConfigStore = configStore;
            Deduplicator = deduplicator;
            _provider = provider;
        }

        public TelegramBotManager Manager { get; }

        public BotRegistry Registry { get; }

        public FakeTelegramBotSettingsStore SettingsStore { get; }

        public FakeTelegramBotConfigStore ConfigStore { get; }

        public FakeTelegramUpdateDeduplicator Deduplicator { get; }

        public void Dispose()
        {
            _provider.Dispose();
        }
    }
}
