// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Hosting.AspNetCore.Tests;

/// <summary>
/// 宿主的本地化集成：<c>Accept-Language</c> 协商进 Baggage，Problem Details 的 <c>detail</c> 按调用方区域翻译；没有装配本地化时退回键与参数的描述。
/// </summary>
public sealed class LocalizationTests
{
    /// <summary>
    /// 业务目录：只有英文与简体中文，用来验证回退到默认区域。
    /// </summary>
    private static readonly MessageCatalog _orders = new("orders", new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
        ["en-US"] = new Dictionary<string, string> { ["orders.order.not_found"] = "Order '{id}' was not found." },
        ["zh-CN"] = new Dictionary<string, string> { ["orders.order.not_found"] = "订单 '{id}' 不存在。" },
    });

    /// <summary>
    /// 启动测试宿主：<c>/missing</c> 写入带 <c>id</c> 参数的未找到错误，<c>/locale</c> 回显 Baggage 里的区域。
    /// </summary>
    /// <param name="localization">是否装配内存本地化特性。</param>
    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(bool localization)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddMessageCatalog(_orders);
        builder.AddXiHan(xihan =>
        {
            xihan.UseFeature(new StubFeature(TestManifests.Create("xihan.test.localization")));
            if (localization)
            {
                xihan.UseFeature<InMemoryLocalizationFeature>();
            }
        });

        var app = builder.Build();
        app.UseXiHan();
        app.MapGet("/missing", (HttpContext http) =>
        {
            http.GetXiHanContext().ValueOrThrow().SetOutcome(Error.NotFound("ORDERS_NOTFOUND_001", "orders.order.not_found", MessageArguments.Of(("id", 42))));
            return Results.Empty;
        });
        app.MapGet("/locale", (HttpContext http) => http.GetXiHanContext().ValueOrThrow().TryGetBaggage(BaggageKeys.Locale, out var locale) ? locale : "-");
        app.MapGet("/text", (HttpContext http) => "orders.order.not_found".L(("id", 42)));
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    /// <summary>
    /// 发送 GET 请求，可带 <c>Accept-Language</c> 与 <c>baggage</c> 头。
    /// </summary>
    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string path, string? acceptLanguage = null, string? baggage = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (acceptLanguage is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);
        }

        if (baggage is not null)
        {
            request.Headers.TryAddWithoutValidation("baggage", baggage);
        }

        return client.SendAsync(request);
    }

    /// <summary>
    /// 读 Problem Details 的 <c>detail</c>。
    /// </summary>
    private static async Task<string> DetailOf(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.GetProperty("detail").GetString()!;
    }

    [Fact]
    public async Task AcceptLanguage_IsNegotiatedIntoLocaleBaggage()
    {
        var (app, client) = await StartAsync(localization: true);
        await using var _ = app;

        // zh-HK 未启用，按语言命中 zh-CN；权重低的 ja 不参与
        Assert.Equal("zh-CN", await (await GetAsync(client, "/locale", acceptLanguage: "zh-HK, ja;q=0.8")).Content.ReadAsStringAsync());
        // 显式 Baggage 优先于 Accept-Language
        Assert.Equal("ko-KR", await (await GetAsync(client, "/locale", acceptLanguage: "zh-CN", baggage: "xihan.locale=ko-KR")).Content.ReadAsStringAsync());
        // 都没有时 Baggage 不写区域，由本地化按默认区域处理
        Assert.Equal("-", await (await GetAsync(client, "/locale")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ProblemDetail_IsLocalizedWithContentLanguage_AndLWorksInsideEndpoints()
    {
        var (app, client) = await StartAsync(localization: true);
        await using var _ = app;

        var chinese = await GetAsync(client, "/missing", acceptLanguage: "zh-CN");
        Assert.Equal(HttpStatusCode.NotFound, chinese.StatusCode);
        Assert.Equal("订单 '42' 不存在。", await DetailOf(chinese));
        Assert.Equal(["zh-CN"], chinese.Content.Headers.ContentLanguage);

        // 目录没有日文，回退到默认区域 en-US 的模板
        var japanese = await GetAsync(client, "/missing", acceptLanguage: "ja-JP");
        Assert.Equal("Order '42' was not found.", await DetailOf(japanese));
        Assert.Equal(["en-US"], japanese.Content.Headers.ContentLanguage);

        var none = await GetAsync(client, "/missing");
        Assert.Equal("Order '42' was not found.", await DetailOf(none));
        Assert.Equal(["en-US"], none.Content.Headers.ContentLanguage);

        // 端点内直接 key.L()：内核已按 Accept-Language 安装了作用域
        Assert.Equal("订单 '42' 不存在。", await (await GetAsync(client, "/text", acceptLanguage: "zh-CN")).Content.ReadAsStringAsync());
        Assert.Equal("Order '42' was not found.", await (await GetAsync(client, "/text")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task WithoutLocalization_DetailIsTheDescription_AndLocaleIsStillPropagated()
    {
        // 本测试要观察没有解析器时的行为，先清掉其他宿主留下的进程默认作用域
        MessageScope.SetDefault(null, null);
        var (app, client) = await StartAsync(localization: false);
        await using var _ = app;

        // 没有本地化选项时不协商，只把第一个合法候选规范化后放进 Baggage
        Assert.Equal("zh-CN", await (await GetAsync(client, "/locale", acceptLanguage: "zh-cn, en;q=0.5")).Content.ReadAsStringAsync());

        var response = await GetAsync(client, "/missing", acceptLanguage: "zh-CN");
        Assert.Equal("orders.order.not_found (id=42)", await DetailOf(response));
        Assert.Empty(response.Content.Headers.ContentLanguage);
        Assert.Equal("orders.order.not_found (id=42)", await (await GetAsync(client, "/text")).Content.ReadAsStringAsync());
    }

    [Fact]
    public void AcceptedLocales_OrderByQuality_AndSkipWildcardZeroAndInvalid()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptLanguage = "en;q=0.5, zh-CN, *;q=0.1, ja;q=0, pt-BR;q=0.5";

        // 同权重保持原顺序：en 在 pt-BR 之前
        Assert.Equal(["zh-CN", "en", "pt-BR"], HttpInvocation.AcceptedLocales(context));
        Assert.Empty(HttpInvocation.AcceptedLocales(new DefaultHttpContext()));
    }
}
