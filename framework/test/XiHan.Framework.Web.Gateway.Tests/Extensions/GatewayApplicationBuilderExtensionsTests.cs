// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Models;
using XiHan.Framework.Web.Gateway.Constants;
using XiHan.Framework.Web.Gateway.Extensions;

namespace XiHan.Framework.Web.Gateway.Tests;

/// <summary>
/// 网关应用构建器扩展测试
/// </summary>
/// <remarks>
/// 这里不起 TestServer，而是直接用 <see cref="ApplicationBuilder"/> 组装真实管道并 Build 出委托，
/// 再用 <see cref="DefaultHttpContext"/> 驱动一次请求：既验证了中间件顺序（异常处理必须在最外层），
/// 也避免了宿主启动带来的开销和不确定性。
/// </remarks>
public class GatewayApplicationBuilderExtensionsTests
{
    /// <summary>
    /// UseGateway 返回同一个构建器以支持链式调用
    /// </summary>
    [Fact]
    public void UseGateway_ReturnsSameApplicationBuilder()
    {
        using var provider = BuildProvider();
        var app = new ApplicationBuilder(provider);

        var returned = app.UseGateway();

        Assert.Same(app, returned);
    }

    /// <summary>
    /// UseGrayRouting 返回同一个构建器以支持链式调用
    /// </summary>
    [Fact]
    public void UseGrayRouting_ReturnsSameApplicationBuilder()
    {
        using var provider = BuildProvider();
        var app = new ApplicationBuilder(provider);

        var returned = app.UseGrayRouting();

        Assert.Same(app, returned);
    }

    /// <summary>
    /// UseRequestTracing 返回同一个构建器以支持链式调用
    /// </summary>
    [Fact]
    public void UseRequestTracing_ReturnsSameApplicationBuilder()
    {
        using var provider = BuildProvider();
        var app = new ApplicationBuilder(provider);

        var returned = app.UseRequestTracing();

        Assert.Same(app, returned);
    }

    /// <summary>
    /// UseGateway 组装出的管道同时提供追踪与灰度决策
    /// </summary>
    [Fact]
    public async Task UseGateway_Pipeline_ProvidesTraceIdAndGrayDecisionToDownstream()
    {
        var decision = GrayDecision.Gray("v2", "rule-1");
        using var provider = BuildProvider(decision);
        var app = new ApplicationBuilder(provider);
        app.UseGateway();

        object? seenDecision = null;
        string? seenTraceId = null;
        app.Run(ctx =>
        {
            seenDecision = ctx.Items[GatewayConstants.GrayDecisionKey];
            seenTraceId = ctx.Items[GatewayConstants.TraceIdKey]?.ToString();
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });
        var pipeline = app.Build();
        var (context, _) = CreateContext(provider);

        await pipeline(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Same(decision, seenDecision);
        Assert.False(string.IsNullOrEmpty(seenTraceId));
    }

    /// <summary>
    /// UseGateway 把下游异常转换成带 TraceId 的网关错误体
    /// </summary>
    /// <remarks>
    /// 这条用例同时锁住中间件顺序：异常中间件在最外层才能兜住下游异常，
    /// 追踪中间件在它内层才能让错误体里的 traceId 与响应头一致。
    /// </remarks>
    [Fact]
    public async Task UseGateway_Pipeline_TranslatesDownstreamExceptionToGatewayError()
    {
        using var provider = BuildProvider();
        var app = new ApplicationBuilder(provider);
        app.UseGateway();
        app.Run(_ => throw new ArgumentException("下游参数不合法"));
        var pipeline = app.Build();
        var (context, body) = CreateContext(provider);

        await pipeline(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var traceId = context.Response.Headers[GatewayConstants.Headers.TraceId].ToString();
        Assert.False(string.IsNullOrEmpty(traceId));

        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(body.ToArray()));
        var root = document.RootElement;
        Assert.Equal("GATEWAY_ERROR", root.GetProperty("errorCode").GetString());
        Assert.Equal("下游参数不合法", root.GetProperty("errorMessage").GetString());
        Assert.Equal("/api/orders", root.GetProperty("path").GetString());
        Assert.Equal(traceId, root.GetProperty("traceId").GetString());
    }

    /// <summary>
    /// UseRequestTracing 只装追踪中间件，不带灰度决策
    /// </summary>
    [Fact]
    public async Task UseRequestTracing_Pipeline_OnlyInjectsTraceId()
    {
        using var provider = BuildProvider();
        var app = new ApplicationBuilder(provider);
        app.UseRequestTracing();
        app.Run(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });
        var pipeline = app.Build();
        var (context, _) = CreateContext(provider);

        await pipeline(context);

        Assert.True(context.Items.ContainsKey(GatewayConstants.TraceIdKey));
        Assert.False(context.Items.ContainsKey(GatewayConstants.GrayDecisionKey));
    }

    /// <summary>
    /// UseGrayRouting 只装灰度中间件，不注入 TraceId
    /// </summary>
    [Fact]
    public async Task UseGrayRouting_Pipeline_OnlyInjectsGrayDecision()
    {
        using var provider = BuildProvider();
        var app = new ApplicationBuilder(provider);
        app.UseGrayRouting();
        app.Run(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });
        var pipeline = app.Build();
        var (context, _) = CreateContext(provider);

        await pipeline(context);

        Assert.True(context.Items.ContainsKey(GatewayConstants.GrayDecisionKey));
        Assert.False(context.Items.ContainsKey(GatewayConstants.TraceIdKey));
    }

    /// <summary>
    /// 单独使用灰度中间件时下游异常不会被转成网关错误体
    /// </summary>
    /// <remarks>
    /// 说明异常兜底能力只来自 UseGateway 里的异常中间件，不是灰度中间件自带的。
    /// </remarks>
    [Fact]
    public async Task UseGrayRouting_Pipeline_DoesNotSwallowDownstreamException()
    {
        using var provider = BuildProvider();
        var app = new ApplicationBuilder(provider);
        app.UseGrayRouting();
        app.Run(_ => throw new ArgumentException("下游参数不合法"));
        var pipeline = app.Build();
        var (context, _) = CreateContext(provider);

        await Assert.ThrowsAsync<ArgumentException>(() => pipeline(context));
    }

    /// <summary>
    /// 构造承载网关中间件依赖的服务提供者
    /// </summary>
    private static ServiceProvider BuildProvider(IGrayDecision? decision = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IGrayRuleEngine>(new RecordingGrayRuleEngine(decision));
        // 灰度中间件的 InvokeAsync 声明了 ICurrentTenant 形参，UseMiddleware 会按需从容器解析，
        // 即使形参有默认值也必须注册，否则请求期直接抛「无法解析服务」。
        services.AddSingleton<ICurrentTenant>(new FakeCurrentTenant(88));
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 构造可读回响应体的请求上下文
    /// </summary>
    private static (DefaultHttpContext Context, MemoryStream Body) CreateContext(IServiceProvider provider)
    {
        var body = new MemoryStream();
        var context = new DefaultHttpContext
        {
            RequestServices = provider
        };
        context.Request.Method = "GET";
        context.Request.Path = "/api/orders";
        context.Response.Body = body;
        return (context, body);
    }
}
