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
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.InMemory;
using XiHan.Framework.Profiles.Minimal;
using XiHan.Framework.Protocols.CloudEvents.Inbound;

namespace XiHan.Framework.Protocols.CloudEvents.Tests;

/// <summary>
/// 入站请求的大小与 batch 条数上限（ADR-0187）
/// </summary>
/// <remarks>
/// 端点读请求体有上限，batch 里的事件数有上限；超过即 400，一个事件也不处理
/// </remarks>
public sealed class CloudEventsInboundLimitTests
{
    [Fact]
    public async Task BatchOverTheLimit_IsRejected_WithoutHandlingAnyEvent()
    {
        var (app, client, log) = await StartAsync();
        await using var host = app;

        var response = await PostAsync(client, CloudEventsHttpBinding.BatchContentType, Batch(101));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(CloudEventsErrors.BatchTooLarge, await CodeOf(response));
        Assert.Empty(log.Records);
    }

    [Fact]
    public async Task BodyOverTheLimit_IsRejected_WithoutHandlingTheEvent()
    {
        var (app, client, log) = await StartAsync();
        await using var host = app;
        var envelope = new MessageEnvelope { Id = "big", Source = "/external", Type = "orders", Data = Encoding.UTF8.GetBytes($"\"{new string('x', 2 * 1024 * 1024)}\"") };

        var response = await PostAsync(client, CloudEventsHttpBinding.StructuredContentType, Encoding.UTF8.GetString(CloudEventsFormat.Encode(envelope)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(CloudEventsErrors.BodyTooLarge, await CodeOf(response));
        Assert.Empty(log.Records);
    }

    [Fact]
    public async Task ConfiguredLimits_AreApplied()
    {
        var (app, client, log) = await StartAsync(("XiHan:Protocols:CloudEvents:MaxBatchSize", "150"), ("XiHan:Protocols:CloudEvents:MaxRequestBodyBytes", "4194304"));
        await using var host = app;

        var response = await PostAsync(client, CloudEventsHttpBinding.BatchContentType, Batch(150));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(150, log.Records.Count);
    }

    /// <summary>
    /// 启动测试宿主
    /// </summary>
    /// <remarks>
    /// Minimal 组合 + 内存消息 + CloudEvents 适配器 + 订阅 orders 的特性
    /// </remarks>
    private static async Task<(WebApplication App, HttpClient Client, DeliveryLog Log)> StartAsync(params (string Key, string Value)[] configuration)
    {
        var log = new DeliveryLog();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(configuration.Select(static c => new KeyValuePair<string, string?>(c.Key, c.Value)));
        builder.AddXiHan(static xihan => xihan.UseMinimalProfile().UseFeature<InMemoryMessagingFeature>().UseFeature<CloudEventsProtocolFeature>().UseFeature<EventsFeature>());
        builder.Services.AddSingleton(log);

        var app = builder.Build();
        app.UseXiHan();
        app.MapXiHanCloudEvents();
        await app.StartAsync();
        return (app, app.GetTestClient(), log);
    }

    /// <summary>
    /// <paramref name="count"/> 个 orders 事件组成的 batch
    /// </summary>
    private static string Batch(int count)
        => "[" + string.Join(',', Enumerable.Range(0, count).Select(static i => Encoding.UTF8.GetString(CloudEventsFormat.Encode(new MessageEnvelope { Id = $"b-{i}", Source = "/external", Type = "orders", Data = "{}"u8.ToArray() })))) + "]";

    /// <summary>
    /// 发一段指定内容类型的正文
    /// </summary>
    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string contentType, string body)
        => client.PostAsync("/events", new ByteArrayContent(Encoding.UTF8.GetBytes(body)) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType) } });

    /// <summary>
    /// 读 Problem Details 的错误码
    /// </summary>
    private static async Task<string> CodeOf(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.GetProperty("code").GetString()!;
    }
}
