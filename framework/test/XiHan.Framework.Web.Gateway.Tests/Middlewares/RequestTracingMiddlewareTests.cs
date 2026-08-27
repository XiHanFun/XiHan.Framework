// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using XiHan.Framework.Web.Gateway.Constants;
using XiHan.Framework.Web.Gateway.Helpers;
using XiHan.Framework.Web.Gateway.Middlewares;

namespace XiHan.Framework.Web.Gateway.Tests;

/// <summary>
/// 请求追踪中间件测试
/// </summary>
/// <remarks>
/// TraceId 的取值有明确的三级优先级：W3C <see cref="Activity"/> &gt; 入站 X-Trace-Id 头 &gt; 宿主 TraceIdentifier。
/// 用例必须自己控制 <see cref="Activity.Current"/>，否则测试宿主里残留的环境 Activity 会让优先级验证时灵时不灵，
/// 因此每个用例都在 try/finally 里显式置空并还原。
/// </remarks>
public class RequestTracingMiddlewareTests
{
    /// <summary>
    /// 无 Activity 时复用入站 TraceId 头，不覆盖成新值
    /// </summary>
    /// <remarks>
    /// 网关是链路的中继节点，覆盖上游传下来的 TraceId 会把一条链路切成两段。
    /// </remarks>
    [Fact]
    public async Task InvokeAsync_WithInboundTraceHeaderAndNoActivity_ReusesInboundTraceId()
    {
        var original = Activity.Current;
        try
        {
            Activity.Current = null;

            var context = new DefaultHttpContext();
            context.Request.Headers[GatewayConstants.Headers.TraceId] = "inbound-trace";
            var middleware = CreateMiddleware(_ => Task.CompletedTask);

            await middleware.InvokeAsync(context);

            Assert.Equal("inbound-trace", context.Response.Headers[GatewayConstants.Headers.TraceId].ToString());
            Assert.Equal("inbound-trace", context.GetTraceId());
        }
        finally
        {
            Activity.Current = original;
        }
    }

    /// <summary>
    /// 既无 Activity 也无入站头时回退到宿主连接标识
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithoutActivityAndWithoutHeader_FallsBackToTraceIdentifier()
    {
        var original = Activity.Current;
        try
        {
            Activity.Current = null;

            var context = new DefaultHttpContext
            {
                TraceIdentifier = "conn-7"
            };
            var middleware = CreateMiddleware(_ => Task.CompletedTask);

            await middleware.InvokeAsync(context);

            Assert.Equal("conn-7", context.Response.Headers[GatewayConstants.Headers.TraceId].ToString());
            Assert.Equal("conn-7", context.GetTraceId());
        }
        finally
        {
            Activity.Current = original;
        }
    }

    /// <summary>
    /// 存在 W3C Activity 时优先使用它的 TraceId
    /// </summary>
    /// <remarks>
    /// 这条优先级与 Web.Api 同源：宿主已经按 traceparent 建好了 Activity，
    /// 自定义头只是兜底通道，不能反过来盖掉标准链路标识。
    /// </remarks>
    [Fact]
    public async Task InvokeAsync_WithCurrentActivity_PrefersW3CTraceId()
    {
        var activity = new Activity("gateway-tracing-test");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();
        try
        {
            var context = new DefaultHttpContext();
            context.Request.Headers[GatewayConstants.Headers.TraceId] = "inbound-trace";
            var middleware = CreateMiddleware(_ => Task.CompletedTask);

            await middleware.InvokeAsync(context);

            var expected = activity.TraceId.ToHexString();
            Assert.Equal(expected, context.Response.Headers[GatewayConstants.Headers.TraceId].ToString());
            Assert.Equal(expected, context.GetTraceId());
            Assert.NotEqual("inbound-trace", context.GetTraceId());
        }
        finally
        {
            activity.Stop();
        }
    }

    /// <summary>
    /// TraceId 在调用下游之前就已经写进响应头和上下文
    /// </summary>
    /// <remarks>
    /// 下游中间件（含异常中间件回写的错误体）都依赖 Items 里的 TraceId，
    /// 如果这步放在 await next 之后，错误体就拿不到 TraceId 了。
    /// </remarks>
    [Fact]
    public async Task InvokeAsync_ExposesTraceIdBeforeCallingNext()
    {
        var original = Activity.Current;
        try
        {
            Activity.Current = null;

            var context = new DefaultHttpContext();
            context.Request.Headers[GatewayConstants.Headers.TraceId] = "inbound-trace";

            string? seenTraceId = null;
            var seenHeader = false;
            var middleware = CreateMiddleware(ctx =>
            {
                seenTraceId = ctx.Items[GatewayConstants.TraceIdKey]?.ToString();
                seenHeader = ctx.Response.Headers.ContainsKey(GatewayConstants.Headers.TraceId);
                return Task.CompletedTask;
            });

            await middleware.InvokeAsync(context);

            Assert.Equal("inbound-trace", seenTraceId);
            Assert.True(seenHeader);
        }
        finally
        {
            Activity.Current = original;
        }
    }

    /// <summary>
    /// 下游异常原样上抛，但追踪信息已经落到响应上
    /// </summary>
    /// <remarks>
    /// 追踪中间件只负责观测，异常必须继续冒泡给外层异常中间件；
    /// 同时 finally 里的耗时日志不能把异常吞掉。
    /// </remarks>
    [Fact]
    public async Task InvokeAsync_WhenNextThrows_RethrowsAndKeepsTraceInfo()
    {
        var original = Activity.Current;
        try
        {
            Activity.Current = null;

            var context = new DefaultHttpContext();
            context.Request.Headers[GatewayConstants.Headers.TraceId] = "inbound-trace";
            var middleware = CreateMiddleware(_ => throw new InvalidOperationException("下游炸了"));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => middleware.InvokeAsync(context));

            Assert.Equal("下游炸了", exception.Message);
            Assert.Equal("inbound-trace", context.Response.Headers[GatewayConstants.Headers.TraceId].ToString());
            Assert.Equal("inbound-trace", context.GetTraceId());
        }
        finally
        {
            Activity.Current = original;
        }
    }

    /// <summary>
    /// 响应头名与网关常量保持一致
    /// </summary>
    /// <remarks>
    /// 中间件里写的是字面量 "X-Trace-Id"，常量类里也有一份；两者一旦分叉，
    /// 按常量取值的业务代码会读到空。
    /// </remarks>
    [Fact]
    public async Task InvokeAsync_UsesHeaderNameFromGatewayConstants()
    {
        var original = Activity.Current;
        try
        {
            Activity.Current = null;

            var context = new DefaultHttpContext
            {
                TraceIdentifier = "conn-7"
            };
            var middleware = CreateMiddleware(_ => Task.CompletedTask);

            await middleware.InvokeAsync(context);

            Assert.True(context.Response.Headers.ContainsKey(GatewayConstants.Headers.TraceId));
            Assert.True(context.Items.ContainsKey(GatewayConstants.TraceIdKey));
        }
        finally
        {
            Activity.Current = original;
        }
    }

    /// <summary>
    /// 下游被调用且响应状态码不被中间件改写
    /// </summary>
    [Fact]
    public async Task InvokeAsync_DoesNotChangeDownstreamStatusCode()
    {
        var original = Activity.Current;
        try
        {
            Activity.Current = null;

            var context = new DefaultHttpContext();
            var invoked = false;
            var middleware = CreateMiddleware(ctx =>
            {
                invoked = true;
                ctx.Response.StatusCode = StatusCodes.Status202Accepted;
                return Task.CompletedTask;
            });

            await middleware.InvokeAsync(context);

            Assert.True(invoked);
            Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
        }
        finally
        {
            Activity.Current = original;
        }
    }

    /// <summary>
    /// 构造中间件
    /// </summary>
    private static RequestTracingMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new RequestTracingMiddleware(next, NullLogger<RequestTracingMiddleware>.Instance);
    }
}
