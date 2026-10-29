// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.InMemory;
using XiHan.Framework.Primitives;
using XiHan.Framework.Profiles.Minimal;
using XiHan.Framework.Protocols.CloudEvents.Inbound;
using XiHan.Framework.Protocols.CloudEvents.Outbound;

namespace XiHan.Framework.Protocols.CloudEvents.Tests;

/// <summary>
/// 入站端点（派发与发布两种模式、主题映射、失败映射）与出站 Webhook 发布者。
/// </summary>
public sealed class CloudEventsProtocolTests
{
    /// <summary>
    /// 启动测试宿主。
    /// </summary>
    /// <remarks>
    /// Minimal 组合 + 内存消息 + CloudEvents 适配器 + 订阅 orders 的特性；出站请求交给捕获处理器。
    /// </remarks>
    private static async Task<(WebApplication App, HttpClient Client, DeliveryLog Log, CapturingHandler Outbound)> StartAsync(params (string Key, string Value)[] configuration)
    {
        var log = new DeliveryLog();
        var outbound = new CapturingHandler();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(configuration.Select(static c => new KeyValuePair<string, string?>(c.Key, c.Value)));
        builder.AddXiHan(static xihan => xihan.UseMinimalProfile().UseFeature<InMemoryMessagingFeature>().UseFeature<CloudEventsProtocolFeature>().UseFeature<EventsFeature>());
        builder.Services.AddSingleton(log);
        builder.Services.AddHttpClient(CloudEventsWebhookPublisher.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => outbound);

        var app = builder.Build();
        app.UseXiHan();
        app.MapXiHanCloudEvents();
        await app.StartAsync();
        return (app, app.GetTestClient(), log, outbound);
    }

    /// <summary>
    /// 发一个 binary 模式的事件。
    /// </summary>
    private static async Task<HttpResponseMessage> PostBinaryAsync(HttpClient client, string type, string id = "evt-1", string? subject = null, params (string Name, string Value)[] extensions)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/events") { Content = new StringContent("""{"id":1}""", Encoding.UTF8, "application/json") };
        request.Headers.TryAddWithoutValidation("ce-specversion", "1.0");
        request.Headers.TryAddWithoutValidation("ce-id", id);
        request.Headers.TryAddWithoutValidation("ce-source", "/external");
        request.Headers.TryAddWithoutValidation("ce-type", type);
        if (subject is not null)
        {
            request.Headers.TryAddWithoutValidation("ce-subject", subject);
        }

        foreach (var (name, value) in extensions)
        {
            request.Headers.TryAddWithoutValidation("ce-" + name, value);
        }

        return await client.SendAsync(request);
    }

    /// <summary>
    /// 发一段指定内容类型的正文。
    /// </summary>
    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string contentType, string body)
        => client.PostAsync("/events", new ByteArrayContent(Encoding.UTF8.GetBytes(body)) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType) } });

    /// <summary>
    /// 读 Problem Details 的错误码。
    /// </summary>
    private static async Task<string> CodeOf(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task Inbound_Dispatch_RunsHandlersInThePipeline_WithContextFromExtensions()
    {
        var (app, client, log, _) = await StartAsync(("XiHan:Protocols:CloudEvents:Topics:orders.placed", "orders"));
        await using var host = app;

        // binary：类型经配置映射到主题，租户与关联扩展进入 Baggage
        var accepted = await PostBinaryAsync(client, "orders.placed", extensions: [("xihantenant", "acme"), ("xihancorrelation", "corr-9")]);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        var (delivery, baggage) = Assert.Single(log.Records);
        Assert.Equal(("orders", "evt-1", "orders.placed", 1), (delivery.Topic, delivery.Envelope.Id, delivery.Envelope.Type, delivery.Attempt));
        Assert.Equal("""{"id":1}""", Encoding.UTF8.GetString(delivery.Envelope.Data.Span));
        Assert.Equal("acme", baggage[BaggageKeys.Tenant]);
        Assert.Equal("corr-9", baggage[BaggageKeys.Correlation]);

        // 类型本身就是合法主题时不需要映射
        Assert.Equal(HttpStatusCode.Accepted, (await PostBinaryAsync(client, "orders", "evt-2")).StatusCode);
        Assert.Equal("orders", log.Records[1].Delivery.Topic);

        // structured 与 batch
        var envelope = new MessageEnvelope { Id = "evt-3", Source = "/external", Type = "orders", Data = "{}"u8.ToArray() };
        Assert.Equal(HttpStatusCode.Accepted, (await PostAsync(client, CloudEventsHttpBinding.StructuredContentType, Encoding.UTF8.GetString(CloudEventsFormat.Encode(envelope)))).StatusCode);
        var batch = $"[{Encoding.UTF8.GetString(CloudEventsFormat.Encode(envelope with { Id = "evt-4" }))},{Encoding.UTF8.GetString(CloudEventsFormat.Encode(envelope with { Id = "evt-5" }))}]";
        Assert.Equal(HttpStatusCode.Accepted, (await PostAsync(client, CloudEventsHttpBinding.BatchContentType, batch)).StatusCode);
        Assert.Equal(["evt-1", "evt-2", "evt-3", "evt-4", "evt-5"], log.Records.Select(static r => r.Delivery.Envelope.Id));
    }

    [Fact]
    public async Task Inbound_Dispatch_MapsFailures()
    {
        var (app, client, log, _) = await StartAsync();
        await using var host = app;

        // 处理器失败：结果按错误 KIND 写成 Problem Details（CONFLICT → 409）
        var rejected = await PostBinaryAsync(client, "orders", subject: "fail");
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal("ORDERS_CONFLICT_001", await CodeOf(rejected));
        Assert.Single(log.Records);

        // 没有处理器的主题、无法解析的类型、不是 CloudEvent、信封不合法
        var idle = await PostBinaryAsync(client, "idle");
        Assert.Equal(HttpStatusCode.NotFound, idle.StatusCode);
        Assert.Equal(CloudEventsErrors.NoSubscriber, await CodeOf(idle));
        var unresolved = await PostBinaryAsync(client, "Orders.Placed");
        Assert.Equal(HttpStatusCode.BadRequest, unresolved.StatusCode);
        Assert.Equal(CloudEventsErrors.TopicUnresolved, await CodeOf(unresolved));
        var plain = await PostAsync(client, "application/json", "{}");
        Assert.Equal(HttpStatusCode.BadRequest, plain.StatusCode);
        Assert.Equal(CloudEventsErrors.UnsupportedContentType, await CodeOf(plain));
        var invalid = await PostAsync(client, CloudEventsHttpBinding.StructuredContentType, """{"specversion":"1.0","id":"x"}""");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(MessagingErrors.InvalidEnvelope, await CodeOf(invalid));

        // batch 里第一个失败即停止：前面的已处理
        var envelope = new MessageEnvelope { Id = "b-1", Source = "/external", Type = "orders", Data = "{}"u8.ToArray() };
        var batch = $"[{Encoding.UTF8.GetString(CloudEventsFormat.Encode(envelope))},{Encoding.UTF8.GetString(CloudEventsFormat.Encode(envelope with { Id = "b-2", Subject = "fail" }))},{Encoding.UTF8.GetString(CloudEventsFormat.Encode(envelope with { Id = "b-3" }))}]";
        var partial = await PostAsync(client, CloudEventsHttpBinding.BatchContentType, batch);
        Assert.Equal(HttpStatusCode.Conflict, partial.StatusCode);
        Assert.Equal(["evt-1", "b-1", "b-2"], log.Records.Select(static r => r.Delivery.Envelope.Id));
    }

    [Fact]
    public async Task Inbound_Publish_HandsEventsToTheBroker()
    {
        var (app, client, log, _) = await StartAsync(("XiHan:Protocols:CloudEvents:Inbound", "publish"));
        await using var host = app;

        // 发布模式：先 202，投递由代理异步完成；没有订阅者的主题也接受
        Assert.Equal(HttpStatusCode.Accepted, (await PostBinaryAsync(client, "orders", extensions: [("xihantenant", "acme")])).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await PostBinaryAsync(client, "idle", "evt-2")).StatusCode);
        await log.WaitAsync(1, TimeSpan.FromSeconds(10));
        var (delivery, baggage) = Assert.Single(log.Records);
        Assert.Equal(("orders", "evt-1"), (delivery.Topic, delivery.Envelope.Id));
        Assert.Equal("acme", baggage[BaggageKeys.Tenant]);
    }

    [Fact]
    public async Task Outbound_WebhookPublisher_PostsCloudEvents_AndMapsResponses()
    {
        var (app, _, _, outbound) = await StartAsync(("XiHan:Protocols:CloudEvents:Webhooks:orders", "https://hooks.test/orders"));
        await using var host = app;
        var publisher = app.Services.GetRequiredService<CloudEventsWebhookPublisher>();
        var envelope = new MessageEnvelope { Id = "evt-1", Source = "/orders", Type = "orders.placed", Subject = "order/1", Data = "{\"id\":1}"u8.ToArray() }.WithExtension(EnvelopeExtensions.Tenant, "acme");

        // binary（默认）
        Assert.True((await publisher.PublishAsync("orders", envelope, CancellationToken.None)).IsSuccess);
        var captured = Assert.Single(outbound.Requests);
        Assert.Equal((HttpMethod.Post, "https://hooks.test/orders"), (captured.Method, captured.Url.ToString()));
        Assert.Equal("1.0", captured.Headers["ce-specversion"]);
        Assert.Equal("evt-1", captured.Headers["ce-id"]);
        Assert.Equal("orders.placed", captured.Headers["ce-type"]);
        Assert.Equal("acme", captured.Headers["ce-xihantenant"]);
        Assert.StartsWith("application/json", captured.Headers["Content-Type"], StringComparison.Ordinal);
        Assert.Equal("""{"id":1}""", Encoding.UTF8.GetString(captured.Body));

        // 没有配置的主题、非 2xx、连不上
        Assert.Equal(CloudEventsErrors.WebhookMissing, (await publisher.PublishAsync("idle", envelope, CancellationToken.None)).Error?.Code);
        outbound.Status = HttpStatusCode.InternalServerError;
        var failed = await publisher.PublishAsync("orders", envelope, CancellationToken.None);
        Assert.Equal(CloudEventsErrors.WebhookFailed, failed.Error?.Code);
        Assert.Equal(500, failed.Error?.Details?["status"]);
        outbound.Unreachable = true;
        Assert.Equal(CloudEventsErrors.WebhookUnreachable, (await publisher.PublishAsync("orders", envelope, CancellationToken.None)).Error?.Code);
    }

    [Fact]
    public async Task Outbound_StructuredMode_SendsTheStructuredFormat()
    {
        var (app, _, _, outbound) = await StartAsync(("XiHan:Protocols:CloudEvents:Webhooks:orders", "https://hooks.test/orders"), ("XiHan:Protocols:CloudEvents:Outbound", "Structured"));
        await using var host = app;
        var envelope = new MessageEnvelope { Id = "evt-1", Source = "/orders", Type = "orders.placed", Data = "{}"u8.ToArray() };

        Assert.True((await app.Services.GetRequiredService<CloudEventsWebhookPublisher>().PublishAsync("orders", envelope, CancellationToken.None)).IsSuccess);
        var captured = Assert.Single(outbound.Requests);
        Assert.StartsWith(CloudEventsHttpBinding.StructuredContentType, captured.Headers["Content-Type"], StringComparison.Ordinal);
        EnvelopeAssert.Same(envelope, CloudEventsFormat.Decode(captured.Body).ValueOrThrow());
    }

    [Fact]
    public async Task Outbound_PublishMany_PostsOneBatchRequest()
    {
        var (app, _, _, outbound) = await StartAsync(("XiHan:Protocols:CloudEvents:Webhooks:orders", "https://hooks.test/orders"));
        await using var host = app;
        var publisher = app.Services.GetRequiredService<CloudEventsWebhookPublisher>();
        var first = new MessageEnvelope { Id = "evt-1", Source = "/orders", Type = "orders.placed", Data = "{\"id\":1}"u8.ToArray() };
        var second = new MessageEnvelope { Id = "evt-2", Source = "/orders", Type = "orders.placed", Data = "{\"id\":2}"u8.ToArray() };

        // 多条：一次 POST、batch 内容模式、数组顺序同入参，一个响应对整批生效
        var results = await publisher.PublishManyAsync("orders", [first, second], CancellationToken.None);
        Assert.Equal(2, results.Count);
        Assert.All(results, static r => Assert.True(r.IsSuccess, r.ToString()));
        var captured = Assert.Single(outbound.Requests);
        Assert.StartsWith(CloudEventsHttpBinding.BatchContentType, captured.Headers["Content-Type"], StringComparison.Ordinal);
        using (var document = JsonDocument.Parse(captured.Body))
        {
            var events = document.RootElement.EnumerateArray().Select(static e => CloudEventsFormat.Decode(Encoding.UTF8.GetBytes(e.GetRawText())).ValueOrThrow()).ToList();
            Assert.Equal(2, events.Count);
            EnvelopeAssert.Same(first, events[0]);
            EnvelopeAssert.Same(second, events[1]);
        }

        // 空集合不发请求；没有配置的主题每一项都是 NOTFOUND；非 2xx 时整批同一失败
        Assert.Empty(await publisher.PublishManyAsync("orders", [], CancellationToken.None));
        Assert.Single(outbound.Requests);
        Assert.All(await publisher.PublishManyAsync("idle", [first, second], CancellationToken.None), static r => Assert.Equal(CloudEventsErrors.WebhookMissing, r.Error?.Code));
        outbound.Status = HttpStatusCode.BadGateway;
        Assert.All(await publisher.PublishManyAsync("orders", [first, second], CancellationToken.None), static r => Assert.Equal(CloudEventsErrors.WebhookFailed, r.Error?.Code));
    }

    [Fact]
    public void Options_ValidateAndParse()
    {
        Assert.Throws<ArgumentException>(static () => new CloudEventsProtocolOptions { Outbound = CloudEventsContentMode.Batch });
        Assert.Throws<ArgumentException>(static () => new CloudEventsProtocolOptions { Topics = new Dictionary<string, string> { ["t"] = "Bad Topic" } });
        Assert.Throws<ArgumentException>(static () => new CloudEventsProtocolOptions { Webhooks = new Dictionary<string, Uri> { ["orders"] = new Uri("/relative", UriKind.Relative) } });
        Assert.Throws<ArgumentException>(static () => new CloudEventsProtocolOptions { Webhooks = new Dictionary<string, Uri> { ["orders"] = new Uri("ftp://x/y") } });

        var options = CloudEventsProtocolOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XiHan:Protocols:CloudEvents:Inbound"] = "publish",
            ["XiHan:Protocols:CloudEvents:Outbound"] = "structured",
            ["XiHan:Protocols:CloudEvents:Topics:orders.placed"] = "orders",
            ["XiHan:Protocols:CloudEvents:Webhooks:orders"] = "https://hooks.test/orders",
        }).Build());
        Assert.Equal((CloudEventsInboundMode.Publish, CloudEventsContentMode.Structured), (options.Inbound, options.Outbound));
        Assert.Equal("orders", options.TopicFor("orders.placed").ValueOrThrow());
        Assert.Equal("orders.shipped", options.TopicFor("orders.shipped").ValueOrThrow());
        Assert.False(options.TopicFor("Orders.Shipped").HasValue);
        Assert.Equal("https://hooks.test/orders", options.Webhooks["orders"].ToString());
        Assert.Throws<ArgumentException>(static () => CloudEventsProtocolOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["XiHan:Protocols:CloudEvents:Inbound"] = "sometimes" }).Build()));
    }
}
