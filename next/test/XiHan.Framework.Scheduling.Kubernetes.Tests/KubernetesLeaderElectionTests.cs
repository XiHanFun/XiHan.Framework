// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.InMemory;
using XiHan.Framework.Scheduling.Kubernetes.Leases;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Scheduling.Kubernetes.Tests;

/// <summary>
/// Lease 语义。
/// </summary>
/// <remarks>
/// 创建即获取、接管过期租约并累加交接次数、续约带版本、释放清空持有者、API 失败翻译为不可达；选项解析与特性装配。
/// </remarks>
public sealed class KubernetesLeaderElectionTests
{
    /// <summary>
    /// 两个 Pod 共用一个 API Server 桩。
    /// </summary>
    private static (FakeLeaseApi Api, KubernetesLeaderElection A, KubernetesLeaderElection B, FakeTimeProvider Clock) Cluster()
    {
        var api = new FakeLeaseApi();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero));
        KubernetesLeaderElection Pod(string name)
        {
            var options = new KubernetesSchedulingOptions { Lease = "orders-scheduler", Namespace = "shop", Holder = name, Token = FakeLeaseApi.Token, ApiServer = new Uri("https://api.test") };
            return new KubernetesLeaderElection(new KubernetesLeaseClient(new HttpClient(api) { BaseAddress = options.ApiServer }, options), options, clock, NullLoggerFactory.Instance);
        }

        return (api, Pod("pod-a"), Pod("pod-b"), clock);
    }

    [Fact]
    public async Task Acquire_CreatesLease_TakesOverWhenExpired_AndRenewsWithVersion()
    {
        var (api, a, b, clock) = Cluster();
        JobName resource = "scheduling.jobs";

        var leaseA = (await a.TryAcquireAsync(resource, TimeSpan.FromSeconds(15), CancellationToken.None)).ValueOrThrow();
        Assert.Equal(("pod-a", 15, 0), ((string?)api.Spec("orders-scheduler")!["holderIdentity"], (int?)api.Spec("orders-scheduler")!["leaseDurationSeconds"], (int?)api.Spec("orders-scheduler")!["leaseTransitions"]));
        Assert.True(leaseA.IsHeld);
        Assert.Equal(clock.GetUtcNow().AddSeconds(15), leaseA.ExpiresAt);

        // 未过期：他人被拒；自己也不可重入
        Assert.Equal(SchedulingErrors.LeaseHeld, (await b.TryAcquireAsync(resource, TimeSpan.FromSeconds(15), CancellationToken.None)).Error?.Code);
        Assert.Equal(SchedulingErrors.LeaseHeld, (await a.TryAcquireAsync(resource, TimeSpan.FromSeconds(15), CancellationToken.None)).Error?.Code);

        // 续约推后到期
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.True((await leaseA.RenewAsync(CancellationToken.None)).IsSuccess);
        Assert.Equal(clock.GetUtcNow().AddSeconds(15), leaseA.ExpiresAt);

        // 过期后被接管：交接次数加一，原持有者续约失败且不再持有
        clock.Advance(TimeSpan.FromSeconds(16));
        Assert.False(leaseA.IsHeld);
        var leaseB = (await b.TryAcquireAsync(resource, TimeSpan.FromSeconds(1.5), CancellationToken.None)).ValueOrThrow();
        Assert.Equal(("pod-b", 2, 1), ((string?)api.Spec("orders-scheduler")!["holderIdentity"], (int?)api.Spec("orders-scheduler")!["leaseDurationSeconds"], (int?)api.Spec("orders-scheduler")!["leaseTransitions"]));
        Assert.Equal(SchedulingErrors.LeaseHeld, (await leaseA.RenewAsync(CancellationToken.None)).Error?.Code);
        Assert.True(leaseB.IsHeld);

        // 释放清空持有者，之后他人立即可取
        await leaseB.ReleaseAsync();
        Assert.False(leaseB.IsHeld);
        Assert.Equal(string.Empty, (string?)api.Spec("orders-scheduler")!["holderIdentity"]);
        Assert.True((await a.TryAcquireAsync(resource, TimeSpan.FromSeconds(5), CancellationToken.None)).IsSuccess);
        Assert.Equal(2, (int?)api.Spec("orders-scheduler")!["leaseTransitions"]);
    }

    [Fact]
    public async Task ApiFailures_AreUnavailable_AndOtherResourcesGetDerivedLeaseNames()
    {
        var (api, a, _, _) = Cluster();
        api.FailNext = true;
        var failed = await a.TryAcquireAsync("scheduling.jobs", TimeSpan.FromSeconds(5), CancellationToken.None);
        Assert.Equal(SchedulingErrors.LeaseUnavailable, failed.Error?.Code);
        Assert.Contains("injected failure", (string)failed.Error!.Details!["reason"]!, StringComparison.Ordinal);

        Assert.True((await a.TryAcquireAsync("reports.nightly", TimeSpan.FromSeconds(5), CancellationToken.None)).IsSuccess);
        Assert.NotNull(api.Spec("orders-scheduler-reports-nightly"));
        Assert.Null(api.Spec("orders-scheduler"));

        var wrongToken = new KubernetesSchedulingOptions { Namespace = "shop", Holder = "x", Token = "bad", ApiServer = new Uri("https://api.test") };
        var unauthorized = new KubernetesLeaderElection(new KubernetesLeaseClient(new HttpClient(api) { BaseAddress = wrongToken.ApiServer }, wrongToken), wrongToken, TimeProvider.System, NullLoggerFactory.Instance);
        Assert.Equal(SchedulingErrors.LeaseUnavailable, (await unauthorized.TryAcquireAsync("scheduling.jobs", TimeSpan.FromSeconds(5), CancellationToken.None)).Error?.Code);
    }

    [Fact]
    public void Options_ParseAndResolve()
    {
        var options = KubernetesSchedulingOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XiHan:Scheduling:Kubernetes:Lease"] = "orders-scheduler",
            ["XiHan:Scheduling:Kubernetes:Namespace"] = "shop",
            ["XiHan:Scheduling:Kubernetes:Holder"] = "pod-7",
            ["XiHan:Scheduling:Kubernetes:ApiServer"] = "https://10.0.0.1:6443",
            ["XiHan:Scheduling:Kubernetes:TokenPath"] = "/tmp/token",
        }).Build());
        Assert.Equal(("orders-scheduler", "shop", "pod-7", "/tmp/token"), (options.Lease, options.ResolveNamespace(), options.ResolveHolder(), options.TokenPath));
        Assert.Equal(new Uri("https://10.0.0.1:6443"), options.ResolveApiServer());
        Assert.Equal("orders-scheduler", options.LeaseNameFor("scheduling.jobs"));
        Assert.Equal("orders-scheduler-reports-nightly", options.LeaseNameFor("reports.nightly"));

        var defaults = new KubernetesSchedulingOptions();
        Assert.Equal("xihan-scheduler", defaults.Lease);
        Assert.False(string.IsNullOrEmpty(defaults.ResolveHolder()));
        Assert.Throws<ArgumentException>(static () => new KubernetesSchedulingOptions { Lease = "Bad_Name" });
        Assert.Throws<ArgumentException>(static () => KubernetesSchedulingOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["XiHan:Scheduling:Kubernetes:ApiServer"] = "nope" }).Build()));
    }

    [Fact]
    public async Task Feature_ReplacesInMemoryElection_AndSchedulerBecomesLeaderThroughTheLease()
    {
        var api = new FakeLeaseApi();
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemorySchedulingFeature>().UseFeature<KubernetesSchedulingFeature>(),
            [
                new("XiHan:Scheduling:InMemory:PollIntervalSeconds", "0.05"),
                new("XiHan:Scheduling:Kubernetes:Lease", "orders-scheduler"),
                new("XiHan:Scheduling:Kubernetes:Namespace", "shop"),
                new("XiHan:Scheduling:Kubernetes:Holder", "pod-a"),
            ],
            services =>
            {
                // 预先注册的客户端替代集群内构造；令牌与 API Server 指向桩
                var options = new KubernetesSchedulingOptions { Namespace = "shop", Holder = "pod-a", Token = FakeLeaseApi.Token, ApiServer = new Uri("https://api.test") };
                services.AddSingleton(new KubernetesLeaseClient(new HttpClient(api) { BaseAddress = options.ApiServer }, options));
            });

        Assert.IsType<KubernetesLeaderElection>(host.Services.GetRequiredService<ILeaderElection>());
        Assert.Equal(["scheduling.jobs"], host.App.Graph["xihan.scheduling.inmemory"].SelectedCapabilities);
        Assert.Equal(["scheduling.leader"], host.App.Graph["xihan.scheduling.kubernetes"].SelectedCapabilities);

        var scheduler = host.Services.GetRequiredService<JobSchedulerEngine>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!scheduler.IsLeader && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.True(scheduler.IsLeader);
        Assert.Equal("pod-a", (string?)api.Spec("orders-scheduler")!["holderIdentity"]);
    }
}
