// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Extensions.DependencyInjection;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;
using XiHan.Framework.Bot.Telegram.Webhook;

namespace XiHan.Framework.Bot.Telegram.Tests.Webhook;

/// <summary>
/// <see cref="TelegramBotWebhookApplicationBuilderExtensions"/> Webhook 中间件注册扩展测试
/// </summary>
/// <remarks>
/// 中间件是约定式的（构造函数注入 RequestDelegate + ILogger，InvokeAsync 参数注入管理器与注册表），
/// 只有真的走一遍 UseMiddleware 组装并执行管道，才能证明它能被容器正确激活；
/// 单独 new 中间件测不出装配问题。
/// </remarks>
public class TelegramBotWebhookApplicationBuilderExtensionsTests
{
    /// <summary>
    /// 应用构建器为空时抛参数空异常
    /// </summary>
    [Fact]
    public void UseTelegramBotWebhook_WhenAppNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TelegramBotWebhookApplicationBuilderExtensions.UseTelegramBotWebhook(null!));
    }

    /// <summary>
    /// 扩展方法返回同一个应用构建器，支持链式调用
    /// </summary>
    [Fact]
    public void UseTelegramBotWebhook_ReturnsSameApplicationBuilder()
    {
        using var provider = BuildProvider();
        var app = new Microsoft.AspNetCore.Builder.ApplicationBuilder(provider);

        var returned = app.UseTelegramBotWebhook();

        Assert.Same(app, returned);
    }

    /// <summary>
    /// 非 Webhook 请求穿过中间件继续走后续管线
    /// </summary>
    [Fact]
    public async Task UseTelegramBotWebhook_PassesThroughNonWebhookRequests()
    {
        using var provider = BuildProvider();
        var app = new Microsoft.AspNetCore.Builder.ApplicationBuilder(provider);

        var terminalReached = false;
        _ = app.UseTelegramBotWebhook();
        _ = app.Use(_ => context =>
        {
            terminalReached = true;
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });

        var pipeline = app.Build();
        var httpContext = new DefaultHttpContext { RequestServices = provider };
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/health";

        await pipeline(httpContext);

        Assert.True(terminalReached);
        Assert.Equal(StatusCodes.Status204NoContent, httpContext.Response.StatusCode);
    }

    /// <summary>
    /// Webhook 请求被中间件接管：未配置密钥时直接 401，且不再进入后续管线
    /// </summary>
    [Fact]
    public async Task UseTelegramBotWebhook_HandlesWebhookRequestsWithFailClosedGuard()
    {
        using var provider = BuildProvider();
        var app = new Microsoft.AspNetCore.Builder.ApplicationBuilder(provider);

        var terminalReached = false;
        _ = app.UseTelegramBotWebhook();
        _ = app.Use(_ => context =>
        {
            terminalReached = true;
            return Task.CompletedTask;
        });

        var pipeline = app.Build();
        var httpContext = new DefaultHttpContext { RequestServices = provider };
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = $"{TelegramBotPlatformConsts.DefaultWebhookRoutePrefix}/main-bot";
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("""{"update_id":42}"""));

        await pipeline(httpContext);

        Assert.False(terminalReached);
        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
    }

    /// <summary>
    /// 构造带 Telegram 平台注册的服务提供者
    /// </summary>
    /// <returns>服务提供者</returns>
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton<ITelegramBotSettingsStore>(new FakeTelegramBotSettingsStore());
        _ = services.AddSingleton<ITelegramBotConfigStore>(new FakeTelegramBotConfigStore());
        _ = services.AddXiHanBotTelegramPlatform();

        return services.BuildServiceProvider();
    }
}
