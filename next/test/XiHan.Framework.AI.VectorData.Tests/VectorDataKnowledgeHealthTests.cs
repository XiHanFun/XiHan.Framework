// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.AI.VectorData.Tests;

/// <summary>
/// 健康项 <c>ai.vectordata.store</c>（ADR-0030、ADR-0110）
/// </summary>
/// <remarks>
/// 选项里的探测间隔解析；向量存储应答时 Ready，连接器抛出时 Degraded（<see cref="AIErrors.KnowledgeUnavailable"/>）而应用照常启动与服务，恢复后回到 Ready；
/// 连接器挂起且不理会取消令牌时按间隔超时（<see cref="KernelErrors.HealthProbeFailed"/>）。用可控的替身 <see cref="TestVectorStore"/> 模拟不可达与挂起，用 <see cref="FakeTimeProvider"/> 推进超时，不靠真实等待
/// </remarks>
public sealed class VectorDataKnowledgeHealthTests
{
    /// <summary>
    /// 等异步结果的真实时间上限，只为失败时不挂死
    /// </summary>
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(10);

    [Fact]
    public void Options_ParseHealthProbeInterval_AndRejectValuesThatAreNotFinitePositiveNumbers()
    {
        static IConfiguration Build(params (string Key, string Value)[] values)
            => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

        Assert.Equal(TimeSpan.FromSeconds(15), VectorDataKnowledgeOptions.FromConfiguration(Build()).HealthProbeInterval);
        Assert.Equal(TimeSpan.FromSeconds(2.5), VectorDataKnowledgeOptions.FromConfiguration(Build(("XiHan:AI:VectorData:HealthProbeIntervalSeconds", "2.5"))).HealthProbeInterval);
        foreach (var invalid in new[] { "0", "-1", "soon", "Infinity", "NaN" })
        {
            Assert.Throws<ArgumentException>(() => VectorDataKnowledgeOptions.FromConfiguration(Build(("XiHan:AI:VectorData:HealthProbeIntervalSeconds", invalid))));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => new VectorDataKnowledgeOptions { HealthProbeInterval = TimeSpan.Zero });
    }

    [Fact]
    public async Task Start_FailsOnAnInvalidProbeInterval()
    {
        // 装配时读选项：值不合法即启动失败，不按缺省处理
        var failure = await Assert.ThrowsAsync<ArgumentException>(() => VectorDataTestHost.StartAsync(new TestVectorStore(), ("XiHan:AI:VectorData:HealthProbeIntervalSeconds", "0")));
        Assert.Contains("HealthProbeIntervalSeconds", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Health_IsReady_WhenTheStoreAnswers_AndTheProbeOnlyAsksAboutTheDefaultCollection()
    {
        var store = new TestVectorStore();
        await using var host = await VectorDataTestHost.StartAsync(store, ("XiHan:AI:Knowledge:Collection", "faq"), ("XiHan:AI:VectorData:HealthProbeIntervalSeconds", "2.5"));

        Assert.Contains(VectorDataKnowledgeFeature.HealthName, new VectorDataKnowledgeFeature().Manifest.Contributes.Health);
        var health = host.App.Health();
        var status = Assert.Single(health.Contributions, static c => c.Name == VectorDataKnowledgeFeature.HealthName);
        Assert.Equal(LifecycleState.Ready, status.State);
        Assert.Null(status.Error);
        Assert.True(health.Ready);
        Assert.Equal(TimeSpan.FromSeconds(2.5), Contributor(host).Interval);

        // 只问默认集合在不在：不列集合、不建集合（问的集合还不存在也算可达）；条目不带数据与说明，不写连接信息
        Assert.Equal(["faq"], store.ExistenceChecks.Distinct());
        Assert.Equal(0, store.Listings);
        Assert.Empty(store.Collections);
        Assert.Null(status.Description);
        Assert.Empty(status.Data);
    }

    [Fact]
    public async Task Health_IsDegraded_WhenTheStoreIsUnreachableAtStartup_AndTheAppStillServes()
    {
        var store = new TestVectorStore { Fault = new HttpRequestException("Connection refused by qdrant-prod.internal:6334.") };
        await using var host = await VectorDataTestHost.StartAsync(store);

        var health = host.App.Health();
        var status = Assert.Single(health.Contributions, static c => c.Name == VectorDataKnowledgeFeature.HealthName);
        Assert.Equal(LifecycleState.Degraded, status.State);
        Assert.Equal(AIErrors.KnowledgeUnavailable, status.Error?.Code);
        Assert.Equal(AIMessages.KnowledgeUnavailable, status.Error?.MessageKey);
        // reason 只写异常类型：异常消息可能带地址，不进健康快照
        Assert.Equal(typeof(HttpRequestException).FullName, status.Error?.Details?["reason"]);
        Assert.DoesNotContain("qdrant-prod", status.Error?.Message ?? string.Empty, StringComparison.Ordinal);
        Assert.Empty(status.Data);
        // 条目 Degraded 只把应用压成 Degraded，不影响就绪（ADR-0030）
        Assert.True(health.Ready);
        Assert.Equal(LifecycleState.Degraded, health.State);
    }

    [Fact]
    public async Task Health_FollowsTheStore_DownAndBackUp()
    {
        var store = new TestVectorStore();
        await using var host = await VectorDataTestHost.StartAsync(store);
        var contributor = Contributor(host);
        Assert.Equal(LifecycleState.Ready, Status(host).State);

        store.Fault = new HttpRequestException("Connection reset by peer.");
        await contributor.ProbeAsync(CancellationToken.None);
        var down = Status(host);
        Assert.Equal(LifecycleState.Degraded, down.State);
        Assert.Equal(AIErrors.KnowledgeUnavailable, down.Error?.Code);

        store.Fault = null;
        await contributor.ProbeAsync(CancellationToken.None);
        var up = Status(host);
        Assert.Equal(LifecycleState.Ready, up.State);
        Assert.Null(up.Error);
    }

    [Fact]
    public async Task Health_LogsTheFullException_WarningOnceForAFailure_ThenDebug()
    {
        var logs = new LogCapture();
        var refused = new HttpRequestException("Connection refused by qdrant-prod.internal:6334.");
        var store = new TestVectorStore { Fault = refused };
        await using var host = await VectorDataTestHost.StartAsync(store, null, logs);
        var contributor = Contributor(host);

        // 首探失败：Warning 带完整异常，排查靠它；健康快照里只有类型
        var first = Assert.Single(ProbeEntries(logs));
        Assert.Equal(LogLevel.Warning, first.Level);
        Assert.Same(refused, first.Exception);

        // 同一种失败持续：Debug，不刷 Warning
        await contributor.ProbeAsync(CancellationToken.None);
        Assert.Equal([LogLevel.Warning, LogLevel.Debug], ProbeEntries(logs).Select(static entry => entry.Level));
        Assert.Same(refused, ProbeEntries(logs)[1].Exception);

        // 异常类型变了是新的失败
        var denied = new UnauthorizedAccessException("Key rejected.");
        store.Fault = denied;
        await contributor.ProbeAsync(CancellationToken.None);
        Assert.Equal(LogLevel.Warning, ProbeEntries(logs)[2].Level);
        Assert.Same(denied, ProbeEntries(logs)[2].Exception);
        Assert.Equal(typeof(UnauthorizedAccessException).FullName, Status(host).Error?.Details?["reason"]);

        // 恢复后再失败也是新的失败
        store.Fault = null;
        await contributor.ProbeAsync(CancellationToken.None);
        Assert.Equal(3, ProbeEntries(logs).Count);
        store.Fault = denied;
        await contributor.ProbeAsync(CancellationToken.None);
        Assert.Equal(LogLevel.Warning, ProbeEntries(logs)[3].Level);
    }

    [Fact]
    public async Task Health_TimesOut_WhenTheStoreHangs_EvenIfItIgnoresCancellation()
    {
        var clock = new FakeTimeProvider();
        var store = new TestVectorStore();
        await using var host = await VectorDataTestHost.StartAsync(store, clock, ("XiHan:AI:VectorData:HealthProbeIntervalSeconds", "5"));
        var contributor = Contributor(host);
        Assert.Equal(LifecycleState.Ready, Status(host).State);

        // 连接器挂起且不看取消令牌：探针按单次时限（= 间隔）结束等待，记为探测超时
        store.Hold = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var probing = contributor.ProbeAsync(CancellationToken.None).AsTask();
        Assert.False(probing.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(5));
        await probing.WaitAsync(_patience);

        var status = Status(host);
        Assert.Equal(LifecycleState.Degraded, status.State);
        Assert.Equal(KernelErrors.HealthProbeFailed, status.Error?.Code);
        Assert.Equal(VectorDataKnowledgeFeature.HealthName, status.Error?.Details?["name"]);
        Assert.True(host.App.Health().Ready);
    }

    /// <summary>
    /// 存储记下的探测日志（带异常的条目）
    /// </summary>
    private static List<(LogLevel Level, string Category, Exception? Exception)> ProbeEntries(LogCapture logs)
        => logs.Entries.Where(static entry => entry.Category == typeof(VectorDataKnowledgeStore).FullName && entry.Exception is not null).ToList();

    /// <summary>
    /// 本特性登记的探针宿主
    /// </summary>
    private static PeriodicHealthContributor Contributor(TestHost host) => host.Services.GetRequiredKeyedService<PeriodicHealthContributor>(VectorDataKnowledgeFeature.HealthName);

    /// <summary>
    /// 健康快照里的本条目
    /// </summary>
    private static ContributorStatus Status(TestHost host) => Assert.Single(host.App.Health().Contributions, static c => c.Name == VectorDataKnowledgeFeature.HealthName);
}
