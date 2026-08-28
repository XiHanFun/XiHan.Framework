// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Security.Claims;
using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Models;
using XiHan.Framework.Web.Gateway.Constants;
using XiHan.Framework.Web.Gateway.Helpers;
using XiHan.Framework.Web.Gateway.Middlewares;

namespace XiHan.Framework.Web.Gateway.Tests;

/// <summary>
/// 灰度路由中间件测试
/// </summary>
/// <remarks>
/// 中间件本身不做任何规则匹配，它的契约只有三条：
/// 把 HttpContext 翻译成 GrayContext、把引擎结果放进 Items、无条件放行到下游。
/// 因此用记录型引擎替身把「翻译结果」直接抓出来断言，而不是去测 Traffic 的匹配算法。
/// </remarks>
public class GrayRoutingMiddlewareTests
{
    /// <summary>
    /// 决策结果注入上下文且继续调用下游
    /// </summary>
    /// <remarks>
    /// 灰度中间件只做决策不做转发，未命中灰度也必须放行，否则整个站点直接断流。
    /// </remarks>
    [Fact]
    public async Task InvokeAsync_Always_StoresDecisionAndCallsNext()
    {
        var decision = GrayDecision.NotGray("没有启用的灰度规则");
        var engine = new RecordingGrayRuleEngine(decision);
        var invoked = false;
        var middleware = CreateMiddleware(engine, _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        });
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.True(invoked);
        Assert.Equal(1, engine.CallCount);
        Assert.Same(decision, context.Items[GatewayConstants.GrayDecisionKey]);
    }

    /// <summary>
    /// 命中灰度时决策同样注入上下文并放行
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenDecisionIsGray_StoresDecisionAndCallsNext()
    {
        var decision = GrayDecision.Gray("v2", "rule-1", "命中规则: 测试规则");
        var engine = new RecordingGrayRuleEngine(decision);
        var invoked = false;
        var middleware = CreateMiddleware(engine, _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        });
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.True(invoked);
        Assert.Same(decision, context.Items[GatewayConstants.GrayDecisionKey]);
        // 中间件写入的键必须能被帮助类原样读出来，这是业务侧唯一的读取方式
        Assert.True(context.IsGrayRequest());
        Assert.Equal("v2", context.GetTargetVersion());
    }

    /// <summary>
    /// 灰度上下文携带请求路径、方法与客户端 IP
    /// </summary>
    [Fact]
    public async Task InvokeAsync_BuildsGrayContextFromRequestLine()
    {
        var engine = new RecordingGrayRuleEngine();
        var middleware = CreateMiddleware(engine, _ => Task.CompletedTask);
        var context = CreateContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/orders";
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.1.2.3");

        await middleware.InvokeAsync(context);

        var grayContext = engine.LastContext;
        Assert.NotNull(grayContext);
        Assert.Equal("/api/orders", grayContext.RequestPath);
        Assert.Equal("POST", grayContext.RequestMethod);
        Assert.Equal("10.1.2.3", grayContext.ClientIpAddress);
    }

    /// <summary>
    /// 没有客户端 IP 时灰度上下文里为空
    /// </summary>
    /// <remarks>
    /// 单元测试、内网直连等场景 RemoteIpAddress 就是 null，这里确认不会因为取 ToString 而崩。
    /// </remarks>
    [Fact]
    public async Task InvokeAsync_WithoutRemoteIpAddress_LeavesClientIpNull()
    {
        var engine = new RecordingGrayRuleEngine();
        var middleware = CreateMiddleware(engine, _ => Task.CompletedTask);
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.NotNull(engine.LastContext);
        Assert.Null(engine.LastContext.ClientIpAddress);
    }

    /// <summary>
    /// 请求头整体复制进灰度上下文且大小写不敏感
    /// </summary>
    [Fact]
    public async Task InvokeAsync_CopiesRequestHeadersCaseInsensitively()
    {
        var engine = new RecordingGrayRuleEngine();
        var middleware = CreateMiddleware(engine, _ => Task.CompletedTask);
        var context = CreateContext();
        context.Request.Headers["X-Device-Type"] = "mobile";
        context.Request.Headers[GatewayConstants.Headers.GrayVersion] = "v2";

        await middleware.InvokeAsync(context);

        Assert.NotNull(engine.LastContext);
        Assert.NotNull(engine.LastContext.Headers);
        Assert.Equal("mobile", engine.LastContext.Headers["x-device-type"]);
        Assert.Equal("v2", engine.LastContext.Headers[GatewayConstants.Headers.GrayVersion]);
    }

    /// <summary>
    /// 用户声明里的数字标识写进灰度上下文
    /// </summary>
    [Theory]
    [InlineData("sub")]
    [InlineData("userId")]
    public async Task InvokeAsync_WithNumericUserClaim_FillsUserId(string claimType)
    {
        var engine = new RecordingGrayRuleEngine();
        var middleware = CreateMiddleware(engine, _ => Task.CompletedTask);
        var context = CreateContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(claimType, "123456")]));

        await middleware.InvokeAsync(context);

        Assert.NotNull(engine.LastContext);
        Assert.True(engine.LastContext.UserId.HasValue);
        Assert.Equal(123456L, engine.LastContext.UserId!.Value);
    }

    /// <summary>
    /// sub 声明优先于 X-User-Id 头
    /// </summary>
    /// <remarks>
    /// 头是客户端可伪造的，令牌声明才是可信来源，优先级颠倒会造成灰度越权。
    /// </remarks>
    [Fact]
    public async Task InvokeAsync_WithSubClaimAndUserIdHeader_PrefersSubClaim()
    {
        var engine = new RecordingGrayRuleEngine();
        var middleware = CreateMiddleware(engine, _ => Task.CompletedTask);
        var context = CreateContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "111")]));
        context.Request.Headers[GatewayConstants.Headers.UserId] = "222";

        await middleware.InvokeAsync(context);

        Assert.NotNull(engine.LastContext);
        Assert.True(engine.LastContext.UserId.HasValue);
        Assert.Equal(111L, engine.LastContext.UserId!.Value);
    }

    /// <summary>
    /// 无声明时退回 X-User-Id 头
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithOnlyUserIdHeader_FillsUserIdFromHeader()
    {
        var engine = new RecordingGrayRuleEngine();
        var middleware = CreateMiddleware(engine, _ => Task.CompletedTask);
        var context = CreateContext();
        context.Request.Headers[GatewayConstants.Headers.UserId] = "222";

        await middleware.InvokeAsync(context);

        Assert.NotNull(engine.LastContext);
        Assert.True(engine.LastContext.UserId.HasValue);
        Assert.Equal(222L, engine.LastContext.UserId!.Value);
    }

    /// <summary>
    /// 用户标识不是数字时留空而不是抛异常
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithNonNumericUserId_LeavesUserIdNull()
    {
        var engine = new RecordingGrayRuleEngine();
        var middleware = CreateMiddleware(engine, _ => Task.CompletedTask);
        var context = CreateContext();
        context.Request.Headers[GatewayConstants.Headers.UserId] = "not-a-number";

        await middleware.InvokeAsync(context);

        Assert.NotNull(engine.LastContext);
        Assert.False(engine.LastContext.UserId.HasValue);
    }

    /// <summary>
    /// 完全匿名的请求灰度上下文里没有用户标识
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithoutAnyUserIdentity_LeavesUserIdNull()
    {
        var engine = new RecordingGrayRuleEngine();
        var middleware = CreateMiddleware(engine, _ => Task.CompletedTask);
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.NotNull(engine.LastContext);
        Assert.False(engine.LastContext.UserId.HasValue);
    }

    /// <summary>
    /// 当前租户有标识时写进灰度上下文
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithCurrentTenant_FillsTenantId()
    {
        var engine = new RecordingGrayRuleEngine();
        var middleware = CreateMiddleware(engine, _ => Task.CompletedTask);
        var context = CreateContext();

        await middleware.InvokeAsync(context, new FakeCurrentTenant(88));

        Assert.NotNull(engine.LastContext);
        Assert.True(engine.LastContext.TenantId.HasValue);
        Assert.Equal(88L, engine.LastContext.TenantId!.Value);
    }

    /// <summary>
    /// 未传当前租户时租户标识为空
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithoutCurrentTenant_LeavesTenantIdNull()
    {
        var engine = new RecordingGrayRuleEngine();
        var middleware = CreateMiddleware(engine, _ => Task.CompletedTask);
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.NotNull(engine.LastContext);
        Assert.False(engine.LastContext.TenantId.HasValue);
    }

    /// <summary>
    /// 当前租户存在但无标识（宿主租户）时租户标识为空
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithHostTenant_LeavesTenantIdNull()
    {
        var engine = new RecordingGrayRuleEngine();
        var middleware = CreateMiddleware(engine, _ => Task.CompletedTask);
        var context = CreateContext();

        await middleware.InvokeAsync(context, new FakeCurrentTenant());

        Assert.NotNull(engine.LastContext);
        Assert.False(engine.LastContext.TenantId.HasValue);
    }

    /// <summary>
    /// 请求中止令牌透传给规则引擎
    /// </summary>
    /// <remarks>
    /// 引擎可能访问远端规则仓储，客户端断开后必须能及时取消，不能挂在那里空转。
    /// </remarks>
    [Fact]
    public async Task InvokeAsync_PassesRequestAbortedToRuleEngine()
    {
        var engine = new RecordingGrayRuleEngine();
        var middleware = CreateMiddleware(engine, _ => Task.CompletedTask);
        var context = CreateContext();
        using var requestAbortedSource = new CancellationTokenSource();
        context.RequestAborted = requestAbortedSource.Token;

        await middleware.InvokeAsync(context);

        Assert.Equal(requestAbortedSource.Token, engine.LastCancellationToken);
        // 兜住「传了 CancellationToken.None」这种看似通过实则没传的情况
        Assert.True(engine.LastCancellationToken.CanBeCanceled);
    }

    /// <summary>
    /// 规则引擎异常向上抛出且不放行下游
    /// </summary>
    /// <remarks>
    /// 这里不吞异常是有意的：交给最外层的网关异常中间件转成统一错误体，
    /// 若在此处静默降级为「未命中灰度」，规则仓储故障会被完全掩盖。
    /// </remarks>
    [Fact]
    public async Task InvokeAsync_WhenRuleEngineThrows_PropagatesAndSkipsNext()
    {
        var invoked = false;
        var middleware = CreateMiddleware(new ThrowingGrayRuleEngine(), _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        });
        var context = CreateContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        Assert.False(invoked);
        Assert.False(context.Items.ContainsKey(GatewayConstants.GrayDecisionKey));
    }

    /// <summary>
    /// 每次请求都重新构建灰度上下文，不跨请求复用
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ForEachRequest_BuildsFreshGrayContext()
    {
        var engine = new RecordingGrayRuleEngine();
        var middleware = CreateMiddleware(engine, _ => Task.CompletedTask);

        var first = CreateContext();
        first.Request.Path = "/api/a";
        await middleware.InvokeAsync(first);
        var firstContext = engine.LastContext;

        var second = CreateContext();
        second.Request.Path = "/api/b";
        await middleware.InvokeAsync(second);
        var secondContext = engine.LastContext;

        Assert.NotNull(firstContext);
        Assert.NotNull(secondContext);
        Assert.NotSame(firstContext, secondContext);
        Assert.Equal("/api/a", firstContext.RequestPath);
        Assert.Equal("/api/b", secondContext.RequestPath);
        Assert.Equal(2, engine.CallCount);
    }

    /// <summary>
    /// 构造中间件
    /// </summary>
    private static GrayRoutingMiddleware CreateMiddleware(IGrayRuleEngine engine, RequestDelegate next)
    {
        return new GrayRoutingMiddleware(next, engine, NullLogger<GrayRoutingMiddleware>.Instance);
    }

    /// <summary>
    /// 构造请求上下文
    /// </summary>
    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/default";
        return context;
    }
}
