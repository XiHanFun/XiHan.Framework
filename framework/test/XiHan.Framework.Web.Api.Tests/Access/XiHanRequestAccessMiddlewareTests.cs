// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using XiHan.Framework.Web.Api.Constants;
using XiHan.Framework.Web.Api.Extensions.DependencyInjection;
using XiHan.Framework.Web.Api.Middlewares;
using XiHan.Framework.Web.Core.Access;

namespace XiHan.Framework.Web.Api.Tests.Access;

/// <summary>
/// 请求访问中间件：按顺序询问闸门，第一个拒绝即以统一信封短路，全部放行才继续
/// </summary>
/// <remarks>
/// 应用的访问策略、维护模式都挂在这里，所以短路顺序、状态码与信封、附加响应头都是契约；
/// 没有注册闸门时必须零影响。
/// </remarks>
public sealed class XiHanRequestAccessMiddlewareTests
{
    /// <summary>
    /// 没有闸门：直接进入下一个中间件
    /// </summary>
    [Fact]
    public async Task NoGates_CallsNext()
    {
        var (context, _) = CreateContext();
        var nextCalled = false;

        await new XiHanRequestAccessMiddleware([]).InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.True(nextCalled);
    }

    /// <summary>
    /// 按 Order 从小到大判定，第一个拒绝即短路，后面的闸门与下一个中间件都不再执行
    /// </summary>
    [Fact]
    public async Task Gates_RunInOrderAndShortCircuitOnDeny()
    {
        var calls = new List<string>();
        var gates = new IRequestAccessGate[]
        {
            new StubGate("third", 30, RequestAccessDecision.Allow, calls),
            new StubGate("first", 10, RequestAccessDecision.Allow, calls),
            new StubGate("second", 20, RequestAccessDecision.Deny(StatusCodes.Status403Forbidden, "访问被安全策略拒绝"), calls)
        };
        var (context, _) = CreateContext();
        var nextCalled = false;

        await new XiHanRequestAccessMiddleware(gates).InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.Equal(["first", "second"], calls);
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    /// <summary>
    /// 拒绝写统一信封：业务码跟状态码、消息、数据、traceId，附加响应头照写
    /// </summary>
    [Fact]
    public async Task Deny_WritesEnvelopeAndHeaders()
    {
        var gate = new StubGate("ban", 0, RequestAccessDecision.Deny(
            StatusCodes.Status429TooManyRequests,
            "登录失败次数过多，当前 IP 已被暂时封禁",
            new { retryAfterSeconds = 120 },
            new Dictionary<string, string> { ["Retry-After"] = "120" }), []);
        var (context, body) = CreateContext();
        context.Items[XiHanWebApiConstants.TraceIdItemKey] = "trace-9";

        await new XiHanRequestAccessMiddleware([gate]).InvokeAsync(context, _ => Task.CompletedTask);

        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.Equal("120", context.Response.Headers["Retry-After"].ToString());
        using var json = JsonDocument.Parse(body.ToArray());
        Assert.Equal(429, json.RootElement.GetProperty("code").GetInt32());
        Assert.Equal("登录失败次数过多，当前 IP 已被暂时封禁", json.RootElement.GetProperty("message").GetString());
        Assert.Equal(120, json.RootElement.GetProperty("data").GetProperty("retryAfterSeconds").GetInt32());
        Assert.Equal("trace-9", json.RootElement.GetProperty("traceId").GetString());
    }

    /// <summary>
    /// 没有对应业务码的状态码按 403 归类，HTTP 状态码照原样
    /// </summary>
    [Fact]
    public async Task Deny_UnknownStatus_UsesForbiddenCode()
    {
        var gate = new StubGate("odd", 0, RequestAccessDecision.Deny(451, "不可用"), []);
        var (context, body) = CreateContext();

        await new XiHanRequestAccessMiddleware([gate]).InvokeAsync(context, _ => Task.CompletedTask);

        Assert.Equal(451, context.Response.StatusCode);
        using var json = JsonDocument.Parse(body.ToArray());
        Assert.Equal(403, json.RootElement.GetProperty("code").GetInt32());
    }

    /// <summary>
    /// 拒绝的状态码须为 4xx 或 5xx，说明不能为空
    /// </summary>
    [Fact]
    public void Deny_ValidatesArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RequestAccessDecision.Deny(200, "x"));
        Assert.Throws<ArgumentException>(() => RequestAccessDecision.Deny(403, " "));
        Assert.True(RequestAccessDecision.Allow.IsAllowed);
    }

    /// <summary>
    /// 中间件按类型注册（IMiddleware），注入全部已注册的闸门
    /// </summary>
    [Fact]
    public async Task Registration_ResolvesMiddlewareWithGates()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddXiHanWebApiSecurity(new ConfigurationBuilder().Build());
        services.AddScoped<IRequestAccessGate>(_ => new StubGate("only", 0, RequestAccessDecision.Deny(503, "系统维护中"), []));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var middleware = scope.ServiceProvider.GetRequiredService<XiHanRequestAccessMiddleware>();
        var (context, _) = CreateContext();
        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        Assert.Equal(503, context.Response.StatusCode);
    }

    private static (DefaultHttpContext Context, MemoryStream Body) CreateContext()
    {
        var body = new MemoryStream();
        var context = new DefaultHttpContext();
        context.Response.Body = body;
        return (context, body);
    }

    /// <summary>
    /// 固定结果、记录调用顺序的闸门
    /// </summary>
    private sealed class StubGate(string name, int order, RequestAccessDecision decision, List<string> calls) : IRequestAccessGate
    {
        public int Order { get; } = order;

        public ValueTask<RequestAccessDecision> EvaluateAsync(HttpContext context, CancellationToken cancellationToken = default)
        {
            calls.Add(name);
            return ValueTask.FromResult(decision);
        }
    }
}
