// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.InMemory;
using XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 插件经 <c>Host.Publish</c> 在回调上下文里发布
/// </summary>
/// <remarks>
/// 元数据的租户整批核对（<c>invocation.md</c> §5，ADR-0237）。以前宿主不读 <c>PublishRequest.metadata</c>：插件处理某个租户的调用时发布的消息不带租户、主体与关联 ID，与进程内的发布不同。EchoPlugin 的 <c>echoes.publish</c> 转发收到的元数据，载荷给了 <c>tenant</c> 时改写其中的租户
/// </remarks>
public sealed class CallbackPublishTests
{
    /// <summary>
    /// 宿主侧记录信封的主题
    /// </summary>
    private const string Topic = "audit.events";

    [Fact]
    public async Task Publish_InTheCallOfATenant_FillsTheEnvelopeFromTheCallbackContext()
    {
        using var copy = MessagingCopy();
        var recorder = new EnvelopeRecorder();
        await using var host = await StartAsync(copy, recorder);

        var published = await EchoPluginHost.InvokeAsync(host, "echoes.publish", """{"envelopes":[{"topic":"audit.events","id":"pub-1","type":"audit.recorded"}]}""", Baggage(("acme", "corr-7")));

        Assert.True(published.Outcome.IsSuccess, published.Outcome.ToString());
        Assert.True(EchoPluginHost.Payload(published).GetProperty("results")[0].GetProperty("ok").GetBoolean(), EchoPluginHost.Payload(published).ToString());
        var envelope = await recorder.WaitAsync("pub-1", TimeSpan.FromSeconds(10));
        Assert.NotNull(envelope);
        // 修之前信封一个扩展都没有：发布不在任何调用里，发布器无从补齐
        Assert.Equal("acme", envelope.Extensions.GetValueOrDefault(EnvelopeExtensions.Tenant));
        Assert.Equal(EchoPluginHost.PluginId, envelope.Extensions.GetValueOrDefault(EnvelopeExtensions.Principal));
        Assert.Equal("corr-7", envelope.Extensions.GetValueOrDefault(EnvelopeExtensions.Correlation));
        Assert.True(TraceParent.TryParse(envelope.Extensions.GetValueOrDefault(EnvelopeExtensions.TraceParent), out _), envelope.Extensions.GetValueOrDefault(EnvelopeExtensions.TraceParent));
    }

    [Fact]
    public async Task Publish_NamingAnotherTenantInTheMetadata_DeniesTheWholeBatch()
    {
        using var copy = MessagingCopy();
        var recorder = new EnvelopeRecorder();
        await using var host = await StartAsync(copy, recorder);

        var published = await EchoPluginHost.InvokeAsync(host, "echoes.publish", """{"envelopes":[{"topic":"audit.events","id":"pub-2","type":"audit.recorded"},{"topic":"audit.events","id":"pub-3","type":"audit.recorded"}],"tenant":"globex"}""", Baggage(("acme", "corr-8")));

        // 修之前元数据不读，两项都以无租户发布成功
        Assert.True(published.Outcome.IsSuccess, published.Outcome.ToString());
        var results = EchoPluginHost.Payload(published).GetProperty("results").EnumerateArray().ToList();
        Assert.Equal(2, results.Count);
        Assert.All(results, static result =>
        {
            Assert.False(result.GetProperty("ok").GetBoolean());
            Assert.Equal(PluginErrors.CallbackDenied, result.GetProperty("code").GetString());
            Assert.Equal(PluginMessages.CallbackTenantDenied, result.GetProperty("messageKey").GetString());
        });
        Assert.Null(await recorder.WaitAsync("pub-2", TimeSpan.FromSeconds(1)));
        Assert.Empty(recorder.Received);
    }

    /// <summary>
    /// 带租户与关联 ID 的 Baggage
    /// </summary>
    private static Dictionary<string, string> Baggage((string Tenant, string Correlation) values)
        => new(StringComparer.Ordinal) { [BaggageKeys.Tenant] = values.Tenant, [BaggageKeys.Correlation] = values.Correlation };

    /// <summary>
    /// 清单另声明 <c>capability:messaging.queue</c> 的示例插件副本
    /// </summary>
    private static PluginCopy MessagingCopy()
    {
        var copy = new PluginCopy(EchoPluginHost.Directory);
        copy.EditManifest(static manifest => manifest.Replace("\"permissions\": [ \"capability:kernel.pipeline\" ]", "\"permissions\": [ \"capability:kernel.pipeline\", \"capability:messaging.queue\" ]", StringComparison.Ordinal));
        return copy;
    }

    /// <summary>
    /// 装配副本、内存消息与记录信封的处理器，并授予发布权限
    /// </summary>
    private static Task<TestHost> StartAsync(PluginCopy copy, EnvelopeRecorder recorder)
        => EchoPluginHost.StartAsync(
            static b => b.UseFeature<InMemoryMessagingFeature>(),
            directory: copy.Directory,
            services: services =>
            {
                services.AddSingleton(recorder);
                services.AddMessageHandler<RecordingEnvelopeHandler>(Topic);
            },
            extra: [("XiHan:Plugins:OutOfProcess:Grants:" + EchoPluginHost.PluginId + ":2", "capability:messaging.queue")]);
}
