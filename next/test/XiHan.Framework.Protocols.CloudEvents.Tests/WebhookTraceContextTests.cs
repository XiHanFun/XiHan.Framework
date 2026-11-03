// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.InMemory;
using XiHan.Framework.Profiles.Minimal;
using XiHan.Framework.Protocols.CloudEvents.Outbound;

namespace XiHan.Framework.Protocols.CloudEvents.Tests;

/// <summary>
/// 出站 Webhook 的 HTTP 追踪头：缺省不传播，显式配置才写
/// </summary>
/// <remarks>
/// 请求经命名客户端真正的主处理器发到本机的接收方，断言接收方实际收到的请求头
/// </remarks>
public sealed class WebhookTraceContextTests
{
    /// <summary>
    /// 追踪上下文的全部请求头：W3C 的三个与 .NET 旧格式的两个
    /// </summary>
    private static readonly string[] _traceHeaders = ["traceparent", "tracestate", "baggage", "Request-Id", "Correlation-Context"];

    /// <summary>
    /// 启动只装出站所需特性的应用，<c>orders</c> 主题推给接收方
    /// </summary>
    /// <remarks>
    /// 不替换命名客户端的主处理器；<paramref name="configureClient"/> 给应用自己对命名客户端的配置。接收方在回环地址上，<paramref name="allowLoopback"/> 决定出站地址策略放不放行
    /// </remarks>
    private static async Task<WebApplication> StartAsync(WebhookReceiver receiver, string? propagate = null, Action<IHttpClientBuilder>? configureClient = null, bool allowLoopback = true)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var settings = new Dictionary<string, string?> { ["XiHan:Protocols:CloudEvents:Webhooks:orders"] = receiver.Url.ToString(), ["XiHan:Security:Outbound:AllowLoopback"] = allowLoopback ? "true" : "false" };
        if (propagate is not null)
        {
            settings["XiHan:Protocols:CloudEvents:PropagateTraceContext"] = propagate;
        }

        builder.Configuration.AddInMemoryCollection(settings);
        builder.AddXiHan(static xihan => xihan.UseMinimalProfile().UseFeature<InMemoryMessagingFeature>().UseFeature<CloudEventsProtocolFeature>());
        configureClient?.Invoke(builder.Services.AddHttpClient(CloudEventsWebhookPublisher.HttpClientName));
        var app = builder.Build();
        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// 在带追踪上下文的 Activity 里发一条事件
    /// </summary>
    /// <remarks>
    /// Activity 是 W3C 格式，带 tracestate 与一项 Baggage；返回它的追踪 ID
    /// </remarks>
    private static async Task<string> PublishInsideAnActivityAsync(WebApplication app)
    {
        using var activity = new Activity("webhook.trace.test").SetIdFormat(ActivityIdFormat.W3C);
        activity.TraceStateString = "vendor=internal";
        activity.AddBaggage("xihan.tenant", "acme");
        activity.Start();
        var result = await app.Services.GetRequiredService<CloudEventsWebhookPublisher>().PublishAsync("orders", new MessageEnvelope { Id = "evt-1", Source = "/orders", Type = "orders.placed" }, CancellationToken.None);
        Assert.True(result.IsSuccess, result.ToString());
        return activity.TraceId.ToHexString();
    }

    [Fact]
    public async Task ByDefault_TheWebhookClientSendsNoTraceContextHeaders()
    {
        await using var receiver = await WebhookReceiver.StartAsync();
        await using var app = await StartAsync(receiver);

        await PublishInsideAnActivityAsync(app);

        var headers = Assert.Single(receiver.Requests);
        Assert.Equal("evt-1", headers["ce-id"]);
        Assert.All(_traceHeaders, name => Assert.False(headers.ContainsKey(name), $"'{name}' must not be sent, but was '{headers.GetValueOrDefault(name)}'."));
    }

    [Fact]
    public async Task ReceiverOnADeniedAddress_IsNotReached_AndTheReasonNamesTheDenial()
    {
        // 第三方的 3xx 能把请求引到内网，所以 Webhook 的客户端过出站地址策略（ADR-0117）
        await using var receiver = await WebhookReceiver.StartAsync();
        await using var app = await StartAsync(receiver, allowLoopback: false);

        var result = await app.Services.GetRequiredService<CloudEventsWebhookPublisher>().PublishAsync("orders", new MessageEnvelope { Id = "evt-1", Source = "/orders", Type = "orders.placed" }, CancellationToken.None);

        Assert.Equal(CloudEventsErrors.WebhookUnreachable, result.Error?.Code);
        Assert.Equal(typeof(OutboundAddressDeniedException).FullName, result.Error?.Details?["reason"]);
        Assert.Empty(receiver.Requests);
    }

    [Fact]
    public async Task ByDefault_AnAppConfiguringTheSameSocketsHandler_KeepsTheDefault()
    {
        // 应用用 UseSocketsHttpHandler 调连接参数：配置的是同一个处理器，缺省的不传播保留
        await using var receiver = await WebhookReceiver.StartAsync();
        await using var app = await StartAsync(receiver, configureClient: static client => client.UseSocketsHttpHandler(static (handler, _) => handler.PooledConnectionLifetime = TimeSpan.FromMinutes(1)));

        await PublishInsideAnActivityAsync(app);

        var headers = Assert.Single(receiver.Requests);
        Assert.All(_traceHeaders, name => Assert.False(headers.ContainsKey(name), $"'{name}' must not be sent, but was '{headers.GetValueOrDefault(name)}'."));
    }

    [Fact]
    public async Task PropagateTraceContext_SendsTheCurrentTraceContext()
    {
        await using var receiver = await WebhookReceiver.StartAsync();
        await using var app = await StartAsync(receiver, propagate: "true");

        var traceId = await PublishInsideAnActivityAsync(app);

        var headers = Assert.Single(receiver.Requests);
        Assert.Contains(traceId, headers["traceparent"], StringComparison.Ordinal);
        Assert.Equal("vendor=internal", headers["tracestate"]);
    }

    [Fact]
    public void PropagateTraceContext_IsReadFromConfiguration_AndOnlyAcceptsBooleans()
    {
        static CloudEventsProtocolOptions Read(string? value) => CloudEventsProtocolOptions.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["XiHan:Protocols:CloudEvents:PropagateTraceContext"] = value })
            .Build());

        Assert.False(Read(null).PropagateTraceContext);
        Assert.False(new CloudEventsProtocolOptions().PropagateTraceContext);
        Assert.True(Read("true").PropagateTraceContext);
        Assert.False(Read("False").PropagateTraceContext);
        Assert.Throws<ArgumentException>(static () => Read("yes"));
    }
}
