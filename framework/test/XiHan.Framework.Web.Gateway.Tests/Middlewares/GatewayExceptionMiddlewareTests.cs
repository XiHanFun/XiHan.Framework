// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Web.Gateway.Constants;
using XiHan.Framework.Web.Gateway.Middlewares;

namespace XiHan.Framework.Web.Gateway.Tests;

/// <summary>
/// 网关异常处理中间件测试
/// </summary>
/// <remarks>
/// 中间件直接用 <see cref="DefaultHttpContext"/> 加手写下游委托驱动，不起 TestServer：
/// 它的全部对外契约就是「异常类型 -> 状态码」的映射和错误响应体结构，与真实管道无关。
/// </remarks>
public class GatewayExceptionMiddlewareTests
{
    /// <summary>
    /// 下游正常返回时不改动响应
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenNextSucceeds_LeavesResponseUntouched()
    {
        var (context, body) = CreateContext();
        var invoked = false;
        var middleware = CreateMiddleware(ctx =>
        {
            invoked = true;
            ctx.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(invoked);
        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
        Assert.Equal(string.Empty, ReadBody(body));
    }

    /// <summary>
    /// 下游异常按类型映射到状态码
    /// </summary>
    /// <remarks>
    /// ArgumentNullException / ArgumentOutOfRangeException 是 ArgumentException 的子类，
    /// switch 的类型模式对子类同样命中，这里显式锁住这层继承语义。
    /// </remarks>
    [Theory]
    [InlineData("unauthorized", StatusCodes.Status401Unauthorized)]
    [InlineData("argument", StatusCodes.Status400BadRequest)]
    [InlineData("argumentNull", StatusCodes.Status400BadRequest)]
    [InlineData("argumentOutOfRange", StatusCodes.Status400BadRequest)]
    [InlineData("invalidOperation", StatusCodes.Status500InternalServerError)]
    [InlineData("timeout", StatusCodes.Status500InternalServerError)]
    public async Task InvokeAsync_WhenNextThrows_MapsExceptionTypeToStatusCode(string exceptionKind, int expectedStatusCode)
    {
        var (context, _) = CreateContext();
        var middleware = CreateMiddleware(_ => throw CreateException(exceptionKind));

        await middleware.InvokeAsync(context);

        Assert.Equal(expectedStatusCode, context.Response.StatusCode);
    }

    /// <summary>
    /// 下游异常不再向上抛出
    /// </summary>
    /// <remarks>
    /// 异常中间件位于管道最外层，一旦漏抛就会变成宿主的 500 白页，丢掉统一错误体。
    /// </remarks>
    [Fact]
    public async Task InvokeAsync_WhenNextThrows_DoesNotRethrow()
    {
        var (context, body) = CreateContext();
        context.Request.Path = "/api/orders";
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("下游炸了"));

        // 这里不包 try/catch：一旦中间件把异常漏抛出来，用例会直接因未处理异常而失败
        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.NotEqual(string.Empty, ReadBody(body));
    }

    /// <summary>
    /// 异常响应写出小驼峰的网关错误体
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenNextThrows_WritesCamelCaseGatewayErrorBody()
    {
        var (context, body) = CreateContext();
        context.Request.Path = "/api/orders";
        context.Items[GatewayConstants.TraceIdKey] = "trace-from-tracing";
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("下游炸了"));

        await middleware.InvokeAsync(context);

        Assert.Equal("application/json", context.Response.ContentType);

        using var document = JsonDocument.Parse(ReadBody(body));
        var root = document.RootElement;
        Assert.Equal("trace-from-tracing", root.GetProperty("traceId").GetString());
        Assert.Equal("GATEWAY_ERROR", root.GetProperty("errorCode").GetString());
        Assert.Equal("下游炸了", root.GetProperty("errorMessage").GetString());
        Assert.Equal("/api/orders", root.GetProperty("path").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("details").ValueKind);
    }

    /// <summary>
    /// 错误体的时间戳取当前 UTC 时刻
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenNextThrows_StampsUtcTimestamp()
    {
        var (context, body) = CreateContext();
        context.Request.Path = "/api/orders";
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("下游炸了"));

        var before = DateTime.UtcNow;
        await middleware.InvokeAsync(context);
        var after = DateTime.UtcNow;

        using var document = JsonDocument.Parse(ReadBody(body));
        var timestamp = document.RootElement.GetProperty("timestamp").GetDateTime().ToUniversalTime();

        Assert.InRange(timestamp, before.AddSeconds(-5), after.AddSeconds(5));
    }

    /// <summary>
    /// 已有追踪中间件写入的 TraceId 时优先复用
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenTraceIdInItems_ReusesIt()
    {
        var (context, body) = CreateContext();
        context.TraceIdentifier = "conn-42";
        context.Items[GatewayConstants.TraceIdKey] = "trace-from-tracing";
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("下游炸了"));

        await middleware.InvokeAsync(context);

        using var document = JsonDocument.Parse(ReadBody(body));
        Assert.Equal("trace-from-tracing", document.RootElement.GetProperty("traceId").GetString());
    }

    /// <summary>
    /// 没有 TraceId 时回退到宿主的连接标识
    /// </summary>
    /// <remarks>
    /// 单独使用异常中间件（没挂追踪中间件）时错误体也必须带上可定位的标识。
    /// </remarks>
    [Fact]
    public async Task InvokeAsync_WhenTraceIdMissing_FallsBackToTraceIdentifier()
    {
        var (context, body) = CreateContext();
        context.TraceIdentifier = "conn-42";
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("下游炸了"));

        await middleware.InvokeAsync(context);

        using var document = JsonDocument.Parse(ReadBody(body));
        Assert.Equal("conn-42", document.RootElement.GetProperty("traceId").GetString());
    }

    /// <summary>
    /// 错误码固定为 GATEWAY_ERROR，不随异常类型变化
    /// </summary>
    /// <remarks>
    /// 状态码区分异常类别，错误码只标识「问题出在网关层」，客户端据此区分网关错误与业务错误。
    /// </remarks>
    [Theory]
    [InlineData("unauthorized")]
    [InlineData("argument")]
    [InlineData("invalidOperation")]
    public async Task InvokeAsync_WhenNextThrows_AlwaysUsesGatewayErrorCode(string exceptionKind)
    {
        var (context, body) = CreateContext();
        context.Request.Path = "/api/orders";
        var middleware = CreateMiddleware(_ => throw CreateException(exceptionKind));

        await middleware.InvokeAsync(context);

        using var document = JsonDocument.Parse(ReadBody(body));
        Assert.Equal("GATEWAY_ERROR", document.RootElement.GetProperty("errorCode").GetString());
    }

    /// <summary>
    /// 下游已写入的状态码会被异常映射结果覆盖
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenNextSetStatusThenThrew_OverwritesStatusCode()
    {
        var (context, _) = CreateContext();
        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            throw new UnauthorizedAccessException("令牌已过期");
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    /// <summary>
    /// 按种类构造异常
    /// </summary>
    private static Exception CreateException(string exceptionKind)
    {
        return exceptionKind switch
        {
            "unauthorized" => new UnauthorizedAccessException("无权访问"),
            "argument" => new ArgumentException("参数不合法"),
            "argumentNull" => new ArgumentNullException("name"),
            "argumentOutOfRange" => new ArgumentOutOfRangeException("page"),
            "invalidOperation" => new InvalidOperationException("状态不合法"),
            "timeout" => new TimeoutException("下游超时"),
            _ => new Exception("未分类异常")
        };
    }

    /// <summary>
    /// 构造中间件
    /// </summary>
    private static GatewayExceptionMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new GatewayExceptionMiddleware(next, NullLogger<GatewayExceptionMiddleware>.Instance);
    }

    /// <summary>
    /// 构造可读回响应体的请求上下文
    /// </summary>
    private static (DefaultHttpContext Context, MemoryStream Body) CreateContext()
    {
        var body = new MemoryStream();
        var context = new DefaultHttpContext();
        context.Response.Body = body;
        return (context, body);
    }

    /// <summary>
    /// 读回响应体文本
    /// </summary>
    private static string ReadBody(MemoryStream body)
    {
        return Encoding.UTF8.GetString(body.ToArray());
    }
}
