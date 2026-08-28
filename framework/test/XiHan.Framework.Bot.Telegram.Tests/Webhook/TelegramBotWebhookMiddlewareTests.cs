// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Extensions.DependencyInjection;
using XiHan.Framework.Bot.Telegram.MultiBot;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;
using XiHan.Framework.Bot.Telegram.Webhook;

namespace XiHan.Framework.Bot.Telegram.Tests.Webhook;

/// <summary>
/// <see cref="TelegramBotWebhookMiddleware"/> Webhook 中间件测试
/// </summary>
/// <remarks>
/// 这个中间件是整个平台唯一的公网入口，安全语义必须 fail-closed：
/// 未配置密钥一律 401（请求体字段可伪造，不能当鉴权依据）、密钥不匹配 401；
/// 同时它又要对 Telegram「永远返回 200」，否则失败响应会引发重发风暴。
/// 这两条看似矛盾的要求都必须成立，所以每条路径都单独立用例。
/// </remarks>
public class TelegramBotWebhookMiddlewareTests
{
    private const string DefaultPrefix = "/api/telegram-bot/webhook";
    private const string Secret = "s3cr3t";
    private const string MinimalUpdateJson = """{"update_id":42}""";

    /// <summary>
    /// 下一个中间件为空时抛参数空异常
    /// </summary>
    [Fact]
    public void Constructor_WhenNextNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TelegramBotWebhookMiddleware(null!, NullLogger<TelegramBotWebhookMiddleware>.Instance));
    }

    /// <summary>
    /// 日志记录器为空时抛参数空异常
    /// </summary>
    [Fact]
    public void Constructor_WhenLoggerNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TelegramBotWebhookMiddleware(_ => Task.CompletedTask, null!));
    }

    /// <summary>
    /// HTTP 上下文为空时抛参数空异常
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenHttpContextNull_Throws()
    {
        using var harness = CreateHarness();
        var middleware = new TelegramBotWebhookMiddleware(_ => Task.CompletedTask, NullLogger<TelegramBotWebhookMiddleware>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await middleware.InvokeAsync(null!, harness.Manager, harness.Registry));
    }

    /// <summary>
    /// 管理器为空时抛参数空异常
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenManagerNull_Throws()
    {
        using var harness = CreateHarness();
        var middleware = new TelegramBotWebhookMiddleware(_ => Task.CompletedTask, NullLogger<TelegramBotWebhookMiddleware>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await middleware.InvokeAsync(new DefaultHttpContext(), null!, harness.Registry));
    }

    /// <summary>
    /// 注册表为空时抛参数空异常
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenRegistryNull_Throws()
    {
        using var harness = CreateHarness();
        var middleware = new TelegramBotWebhookMiddleware(_ => Task.CompletedTask, NullLogger<TelegramBotWebhookMiddleware>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await middleware.InvokeAsync(new DefaultHttpContext(), harness.Manager, null!));
    }

    /// <summary>
    /// 非 POST 请求不归本中间件管，直接放行给后续管线
    /// </summary>
    /// <param name="method">HTTP 方法</param>
    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task InvokeAsync_WhenNotPost_CallsNext(string method)
    {
        using var harness = CreateHarness();
        var context = CreateRequest($"{DefaultPrefix}/main-bot", method);

        var nextCalled = await InvokeAsync(harness, context);

        Assert.True(nextCalled);
    }

    /// <summary>
    /// 路径不在 Webhook 前缀下时放行给后续管线
    /// </summary>
    /// <param name="path">请求路径</param>
    [Theory]
    [InlineData("/api/other/webhook/main-bot")]
    [InlineData("/health")]
    [InlineData("/")]
    public async Task InvokeAsync_WhenPathNotUnderPrefix_CallsNext(string path)
    {
        using var harness = CreateHarness();
        var context = CreateRequest(path);

        var nextCalled = await InvokeAsync(harness, context);

        Assert.True(nextCalled);
    }

    /// <summary>
    /// 前缀后没有机器人名称时放行给后续管线
    /// </summary>
    /// <param name="path">请求路径</param>
    [Theory]
    [InlineData(DefaultPrefix)]
    [InlineData(DefaultPrefix + "/")]
    public async Task InvokeAsync_WhenBotNameMissing_CallsNext(string path)
    {
        using var harness = CreateHarness();
        var context = CreateRequest(path);

        var nextCalled = await InvokeAsync(harness, context);

        Assert.True(nextCalled);
    }

    /// <summary>
    /// 前缀后带多级路径时不当作机器人名称，放行给后续管线
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenPathHasExtraSegments_CallsNext()
    {
        using var harness = CreateHarness();
        var context = CreateRequest($"{DefaultPrefix}/main-bot/extra");

        var nextCalled = await InvokeAsync(harness, context);

        Assert.True(nextCalled);
    }

    /// <summary>
    /// 未配置 Webhook 密钥时一律拒绝（fail-closed），且不放行给后续管线
    /// </summary>
    /// <remarks>
    /// 这是最关键的一条：密钥留空不代表「不校验」，而是「Webhook 完全不可用」。
    /// 一旦被改成「空 = 放行」，任何人都能伪造 Update 驱动机器人。
    /// </remarks>
    [Fact]
    public async Task InvokeAsync_WhenSecretNotConfigured_ReturnsUnauthorized()
    {
        using var harness = CreateHarness();
        var context = CreateRequest($"{DefaultPrefix}/main-bot", body: MinimalUpdateJson);
        context.Request.Headers[TelegramBotPlatformConsts.SecretTokenHeaderName] = Secret;

        var nextCalled = await InvokeAsync(harness, context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    /// <summary>
    /// 请求头缺少密钥令牌时返回 401
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenSecretHeaderMissing_ReturnsUnauthorized()
    {
        using var harness = await CreateStartedHarnessAsync();
        var context = CreateRequest($"{DefaultPrefix}/main-bot", body: MinimalUpdateJson);

        var nextCalled = await InvokeAsync(harness, context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    /// <summary>
    /// 密钥令牌不匹配时返回 401（大小写敏感的固定时间比较）
    /// </summary>
    /// <param name="actualSecret">请求头携带的密钥</param>
    [Theory]
    [InlineData("wrong-secret")]
    [InlineData("S3CR3T")]
    [InlineData("s3cr3")]
    [InlineData("s3cr3tt")]
    [InlineData("")]
    public async Task InvokeAsync_WhenSecretMismatch_ReturnsUnauthorized(string actualSecret)
    {
        using var harness = await CreateStartedHarnessAsync();
        var context = CreateRequest($"{DefaultPrefix}/main-bot", body: MinimalUpdateJson);
        context.Request.Headers[TelegramBotPlatformConsts.SecretTokenHeaderName] = actualSecret;

        var nextCalled = await InvokeAsync(harness, context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    /// <summary>
    /// 密钥匹配但机器人未注册时仍返回 200，避免 Telegram 重发风暴
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenBotNotRegistered_ReturnsOk()
    {
        using var harness = await CreateStartedHarnessAsync();
        var context = CreateRequest($"{DefaultPrefix}/missing-bot", body: MinimalUpdateJson);
        context.Request.Headers[TelegramBotPlatformConsts.SecretTokenHeaderName] = Secret;
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var nextCalled = await InvokeAsync(harness, context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Empty(harness.Deduplicator.Marked);
    }

    /// <summary>
    /// 请求体不是合法 JSON 时吞掉解析异常并返回 200
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenBodyIsInvalidJson_ReturnsOk()
    {
        using var harness = await CreateStartedHarnessAsync();
        var context = CreateRequest($"{DefaultPrefix}/main-bot", body: "{ this is not json ");
        context.Request.Headers[TelegramBotPlatformConsts.SecretTokenHeaderName] = Secret;
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var nextCalled = await InvokeAsync(harness, context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Empty(harness.Deduplicator.Marked);
    }

    /// <summary>
    /// 密钥匹配且机器人已注册时返回 200 并把 Update 交给管理器
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenAuthorizedAndBotRegistered_ReturnsOk()
    {
        using var harness = await CreateStartedHarnessAsync();
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "main-bot"));
        harness.Registry.AddOrUpdate(bot);

        var context = CreateRequest($"{DefaultPrefix}/main-bot", body: MinimalUpdateJson);
        context.Request.Headers[TelegramBotPlatformConsts.SecretTokenHeaderName] = Secret;
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var nextCalled = await InvokeAsync(harness, context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    /// <summary>
    /// 机器人名称匹配忽略大小写（路径段比较按 ASP.NET Core 默认规则）
    /// </summary>
    [Fact]
    public async Task InvokeAsync_PrefixMatchIgnoresCase()
    {
        using var harness = await CreateStartedHarnessAsync();
        var context = CreateRequest("/API/TELEGRAM-BOT/WEBHOOK/main-bot", body: MinimalUpdateJson);
        context.Request.Headers[TelegramBotPlatformConsts.SecretTokenHeaderName] = Secret;

        var nextCalled = await InvokeAsync(harness, context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    /// <summary>
    /// 自定义 Webhook 前缀生效后，默认前缀不再被拦截
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenCustomPrefixConfigured_OnlyMatchesThatPrefix()
    {
        using var harness = await CreateStartedHarnessAsync(settings => settings.WebhookRoutePrefix = "/hooks/tg");

        var defaultPrefixContext = CreateRequest($"{DefaultPrefix}/main-bot", body: MinimalUpdateJson);
        Assert.True(await InvokeAsync(harness, defaultPrefixContext));

        var customPrefixContext = CreateRequest("/hooks/tg/main-bot", body: MinimalUpdateJson);
        Assert.False(await InvokeAsync(harness, customPrefixContext));
        Assert.Equal(StatusCodes.Status401Unauthorized, customPrefixContext.Response.StatusCode);
    }

    /// <summary>
    /// 构造一条 HTTP 请求上下文
    /// </summary>
    /// <param name="path">请求路径</param>
    /// <param name="method">HTTP 方法</param>
    /// <param name="body">请求体</param>
    /// <returns>HTTP 上下文</returns>
    private static DefaultHttpContext CreateRequest(string path, string method = "POST", string? body = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        if (body is not null)
        {
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        }

        return context;
    }

    /// <summary>
    /// 执行中间件并返回后续管线是否被调用
    /// </summary>
    /// <param name="harness">测试装置</param>
    /// <param name="context">HTTP 上下文</param>
    /// <returns>后续管线是否被调用</returns>
    private static async Task<bool> InvokeAsync(WebhookHarness harness, HttpContext context)
    {
        var nextCalled = false;
        var middleware = new TelegramBotWebhookMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<TelegramBotWebhookMiddleware>.Instance);

        await middleware.InvokeAsync(context, harness.Manager, harness.Registry);
        return nextCalled;
    }

    /// <summary>
    /// 构造未启动的测试装置（此时 Webhook 密钥为空）
    /// </summary>
    /// <param name="configureSettings">平台设置配置委托</param>
    /// <returns>测试装置</returns>
    private static WebhookHarness CreateHarness(Action<TelegramBotSettings>? configureSettings = null)
    {
        var settingsStore = new FakeTelegramBotSettingsStore
        {
            Settings = new TelegramBotSettings
            {
                ManagerRefreshSeconds = 0,
                WebhookSecretToken = Secret
            }
        };
        configureSettings?.Invoke(settingsStore.Settings);

        var deduplicator = new FakeTelegramUpdateDeduplicator();

        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton<ITelegramBotSettingsStore>(settingsStore);
        _ = services.AddSingleton<ITelegramBotConfigStore>(new FakeTelegramBotConfigStore());
        _ = services.AddSingleton<ITelegramUpdateDeduplicator>(deduplicator);
        _ = services.AddXiHanBotTelegramPlatform();

        var provider = services.BuildServiceProvider();

        return new WebhookHarness(
            provider.GetRequiredService<TelegramBotManager>(),
            provider.GetRequiredService<BotRegistry>(),
            deduplicator,
            provider);
    }

    /// <summary>
    /// 构造已启动的测试装置（此时 Webhook 密钥已生效）
    /// </summary>
    /// <param name="configureSettings">平台设置配置委托</param>
    /// <returns>测试装置</returns>
    private static async Task<WebhookHarness> CreateStartedHarnessAsync(Action<TelegramBotSettings>? configureSettings = null)
    {
        var harness = CreateHarness(configureSettings);
        await harness.Manager.StartAsync(TestContext.Current.CancellationToken);
        return harness;
    }

    /// <summary>
    /// Webhook 中间件测试装置
    /// </summary>
    private sealed class WebhookHarness : IDisposable
    {
        private readonly ServiceProvider _provider;

        public WebhookHarness(
            TelegramBotManager manager,
            BotRegistry registry,
            FakeTelegramUpdateDeduplicator deduplicator,
            ServiceProvider provider)
        {
            Manager = manager;
            Registry = registry;
            Deduplicator = deduplicator;
            _provider = provider;
        }

        public TelegramBotManager Manager { get; }

        public BotRegistry Registry { get; }

        public FakeTelegramUpdateDeduplicator Deduplicator { get; }

        public void Dispose()
        {
            _provider.Dispose();
        }
    }
}
