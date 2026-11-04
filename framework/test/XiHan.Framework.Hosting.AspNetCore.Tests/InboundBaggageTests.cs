// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.MultiTenancy.InMemory;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore.Tests;

/// <summary>
/// 网络入站不接受调用方自报的租户与主体（RFC-0003 规则 3、ADR-0121）
/// </summary>
/// <remarks>
/// <c>baggage</c> 头是调用方自己写的：解析器只核对租户存在且启用，不核对调用方属不属于它，所以任何没有租户声明的令牌都能借这个头进入任意租户。入站丢掉这两个键；进程内建上下文时放进去的不受影响
/// </remarks>
public sealed class InboundBaggageTests
{
    [Theory]
    [InlineData("xihan.tenant=acme")]
    [InlineData("xihan.tenant=acme,xihan.principal=mallory")]
    [InlineData(" xihan.tenant = acme ;prop=1 , xihan.principal = mallory ")]
    public async Task HttpCaller_CannotPickATenant_OrClaimAPrincipal(string header)
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/whoami");
        request.Headers.TryAddWithoutValidation("baggage", header);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("tenant=none;baggage=", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task EveryBaggageHeader_IsFiltered()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/whoami");
        request.Headers.TryAddWithoutValidation("baggage", "xihan.correlation=c-1");
        request.Headers.TryAddWithoutValidation("baggage", "xihan.tenant=acme");

        var response = await client.SendAsync(request);

        Assert.Equal("tenant=none;baggage=xihan.correlation", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OtherRegisteredKeys_StillArrive()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/whoami");
        request.Headers.TryAddWithoutValidation("baggage", "xihan.correlation=c-1,xihan.idempotency-key=k-1,xihan.locale=zh-CN,xihan.tenant=acme");

        var response = await client.SendAsync(request);

        Assert.Equal("tenant=none;baggage=xihan.correlation,xihan.idempotency-key,xihan.locale", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InProcessContext_KeepsItsTenant()
    {
        // 消息、作业、工作流、命令行都在进程内建上下文，租户是框架自己放进去的：Tenant 阶段照常解析
        await using var app = await StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, CancellationToken.None, baggage: new Dictionary<string, string>(StringComparer.Ordinal) { [BaggageKeys.Tenant] = "acme" });
        string? tenant = null;

        await scope.ServiceProvider.GetRequiredService<App>().InvokeAsync(context, ctx =>
        {
            tenant = ctx.GetSecurity().Tenant?.Id;
            return ValueTask.CompletedTask;
        });

        Assert.Equal("acme", tenant);
    }

    [Fact]
    public void ParseBaggage_StaysAPlainDecoder()
    {
        // 解析本身不做入站取舍：它也用于读别的载体与往返，取舍只在 CreateContext
        Assert.Equal("acme", HttpInvocation.ParseBaggage("xihan.tenant=acme")[BaggageKeys.Tenant]);
    }

    /// <summary>
    /// 启动装配了多租户的宿主，<c>/whoami</c> 写出解析到的租户与上下文里的 Baggage 键
    /// </summary>
    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection([new("XiHan:MultiTenancy:Tenants:acme:DisplayName", "Acme")]);
        builder.AddXiHan(static xihan => xihan.UseFeature<InMemoryMultiTenancyFeature>());
        var app = builder.Build();
        app.UseXiHan();
        app.MapGet("/whoami", static (HttpContext http) =>
        {
            var context = http.GetXiHanContext().ValueOrThrow();
            var keys = context.Baggage.Keys.Order(StringComparer.Ordinal);
            return $"tenant={context.GetSecurity().Tenant?.Id ?? "none"};baggage={string.Join(",", keys)}";
        });
        await app.StartAsync();
        return app;
    }
}
