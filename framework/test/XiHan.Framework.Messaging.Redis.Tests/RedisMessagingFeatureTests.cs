// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.Redis.Tests;

/// <summary>
/// 特性的清单、启动失败与健康项
/// </summary>
/// <remarks>
/// 清单与连不上的用例不需要 Redis；健康项与绑定覆盖的用例在未设置 <c>XIHAN_TEST_REDIS</c> 时跳过
/// </remarks>
public sealed class RedisMessagingFeatureTests : IAsyncDisposable
{
    /// <summary>
    /// 本用例的键前缀
    /// </summary>
    private readonly string _prefix = RedisTestServer.NewPrefix();

    [Fact]
    public void Manifest_ProvidesOnlyTheQueue_AndReadsTheQueueBindingAsARedisServer()
    {
        var manifest = new RedisMessagingFeature().Manifest;

        Assert.Equal("xihan.messaging.redis", manifest.Id);
        Assert.Equal(ManifestRole.Provider, manifest.Role);
        var provided = Assert.Single(manifest.Provides);
        Assert.Equal(MessagingCapabilities.Queue, provided.Id);
        Assert.Equal(MessagingCapabilities.Version, provided.Version);
        var binding = Assert.Single(manifest.Bindings);
        Assert.Equal("queue", binding.Name);
        Assert.Equal("cache:distributed", binding.Type);
        Assert.False(binding.Required);
        Assert.Equal([RedisMessagingFeature.HealthName], manifest.Contributes!.Health);
    }

    [Fact]
    public async Task Start_FailsWithBrokerUnavailable_WhenRedisIsUnreachable_WithoutLeakingThePassword()
    {
        var failure = await Assert.ThrowsAsync<FrameworkException>(() => TestHost.StartAsync(
            static b => b.UseFeature<RedisMessagingFeature>(),
            [new("XiHan:Messaging:Redis:Configuration", "127.0.0.1:1,connectTimeout=300,connectRetry=0,abortConnect=true,password=hunter2")]));

        Assert.Equal(KernelErrors.FeatureStartFailed, failure.Code);
        var inner = Assert.IsType<FrameworkException>(failure.InnerException);
        Assert.Equal(MessagingErrors.BrokerUnavailable, inner.Code);
        Assert.DoesNotContain("hunter2", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", inner.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Probe_BeforeConnecting_ReportsBrokerUnavailable()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var messaging = new MessagingOptions();
        await using var broker = new RedisMessageBroker(
            new RedisMessagingOptions { Configuration = "127.0.0.1:1" },
            messaging,
            new MessagingInstance(messaging),
            host.App,
            TimeProvider.System,
            NullLoggerFactory.Instance);

        var result = await broker.ProbeAsync(CancellationToken.None);

        Assert.False(broker.IsConnected);
        Assert.Equal(MessagingErrors.BrokerUnavailable, result.Error?.Code);
    }

    [Fact]
    public async Task Health_IsReady_AndTheBrokerIsBound_AgainstARealServer()
    {
        var configuration = RedisTestServer.RequireConfiguration();
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<RedisMessagingFeature>(),
            [new("XiHan:Messaging:Redis:Configuration", configuration), new("XiHan:Messaging:Redis:KeyPrefix", _prefix)]);

        var health = host.App.Health();
        var status = Assert.Single(health.Contributions, static c => c.Name == RedisMessagingFeature.HealthName);
        Assert.Equal(LifecycleState.Ready, status.State);
        Assert.Null(status.Error);
        Assert.True(health.Ready);
        Assert.IsType<RedisMessageBroker>(host.Services.GetRequiredService<IMessageBroker>());
    }

    [Fact]
    public async Task Binding_OverridesTheConfiguredConnection_AgainstARealServer()
    {
        var configuration = RedisTestServer.RequireConfiguration();
        // 配置节指向连不上的地址，真实地址只由绑定 queue 给出：能启动说明绑定的主值覆盖了配置节（RFC-0016）
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<RedisMessagingFeature>(),
            [
                new("XiHan:Messaging:Redis:Configuration", "127.0.0.1:1,connectTimeout=300"),
                new("XiHan:Bindings:queue:type", "cache:distributed"),
                new("XiHan:Bindings:queue:value", configuration),
                new("XiHan:Messaging:Redis:KeyPrefix", _prefix),
            ]);

        Assert.True(host.Services.GetRequiredService<RedisMessageBroker>().IsConnected);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await RedisTestServer.DeleteKeysAsync(_prefix);
}
