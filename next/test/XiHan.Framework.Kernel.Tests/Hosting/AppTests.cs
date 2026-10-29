// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Tests.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Tests.Hosting;

/// <summary>
/// <see cref="App"/> 与 <see cref="AppBuilder"/> 的端到端行为。
/// </summary>
/// <remarks>
/// 生命周期顺序、失败策略、停止超时、管道调用、绑定与提供者选择、构建期约束。
/// </remarks>
public sealed class AppTests
{
    [Fact]
    public async Task Lifecycle_StartsInTopologicalOrderAndStopsInReverse()
    {
        var log = new List<string>();
        var caching = new RecordingFeature(Manifests.Create("xihan.caching.inmemory", provides: ["caching.local@1"], requires: ["kernel.hosting@1"]), log);
        var app = new RecordingFeature(Manifests.Create("xihan.app.main", requires: ["caching.local@1"]), log);
        // 故意按依赖者在前的顺序加入，验证顺序由图谱而不是 UseFeature 的调用顺序决定。
        var (provider, instance) = Hosts.BuildApp(b => b.UseFeature(app).UseFeature(caching));
        using var _ = provider;

        Assert.Equal(LifecycleState.Configured, instance.State);
        Assert.False(instance.Health().Ready);

        await instance.StartAsync(CancellationToken.None);
        Assert.Equal(LifecycleState.Ready, instance.State);
        Assert.True(instance.Health().Started);
        Assert.True(instance.Health().Ready);
        Assert.True(instance.Health().Live);

        await instance.StopAsync(CancellationToken.None);
        Assert.Equal(LifecycleState.Stopped, instance.State);
        Assert.Equal(
            ["configure:xihan.caching.inmemory", "configure:xihan.app.main", "start:xihan.caching.inmemory", "start:xihan.app.main", "stop:xihan.app.main", "stop:xihan.caching.inmemory"],
            log);
    }

    [Fact]
    public async Task OptionalDependencyMissing_LeavesFeatureAndAppDegraded()
    {
        var feature = new RecordingFeature(Manifests.Create("xihan.app.main", requires: ["multitenancy.context@1?"]), []);
        var (provider, app) = Hosts.BuildApp(b => b.UseFeature(feature));
        using var _ = provider;

        await app.StartAsync(CancellationToken.None);

        Assert.Equal(LifecycleState.Degraded, app.State);
        Assert.True(app.Health().Ready);
        Assert.Equal(LifecycleState.Degraded, Assert.Single(app.Features).State);
    }

    [Fact]
    public async Task StartFailure_TerminatePolicy_ThrowsAndMarksFailed()
    {
        var log = new List<string>();
        var broken = new RecordingFeature(Manifests.Create("xihan.test.broken"), log) { OnStart = _ => throw new InvalidOperationException("boom") };
        var (provider, app) = Hosts.BuildApp(b => b.UseFeature(broken));
        using var _ = provider;

        var exception = await Assert.ThrowsAsync<FrameworkException>(() => app.StartAsync(CancellationToken.None));

        Assert.Equal(KernelErrors.FeatureStartFailed, exception.Code);
        Assert.Equal(LifecycleState.Failed, app.State);
        Assert.False(app.Health().Live);
        Assert.Equal(KernelErrors.FeatureStartFailed, Assert.Single(app.Features).Error!.Code);
    }

    [Fact]
    public async Task StartFailure_IsolatePolicy_ContinuesOthersAndReportsDegraded()
    {
        var log = new List<string>();
        var broken = new RecordingFeature(Manifests.Create("xihan.test.broken"), log) { OnStart = _ => throw new InvalidOperationException("boom") };
        var healthy = new RecordingFeature(Manifests.Create("xihan.test.healthy"), log);
        var (provider, app) = Hosts.BuildApp(b => b
            .UseFeature(broken)
            .UseFeature(healthy)
            .UseOptions(new KernelOptions { FailurePolicy = FailurePolicy.IsolateFeature }));
        using var _ = provider;

        await app.StartAsync(CancellationToken.None);

        Assert.Contains("start:xihan.test.healthy", log);
        Assert.Equal(LifecycleState.Degraded, app.State);
        Assert.True(app.Health().Live);
        Assert.Equal(LifecycleState.Failed, app.Features.Single(static f => f.Manifest.Id == "xihan.test.broken").State);
    }

    [Fact]
    public async Task StopTimeout_MarksFeatureFailedButStopsOthers()
    {
        var log = new List<string>();
        var slow = new RecordingFeature(Manifests.Create("xihan.test.slow"), log) { OnStop = async token => await Task.Delay(Timeout.InfiniteTimeSpan, token) };
        var quick = new RecordingFeature(Manifests.Create("xihan.test.quick"), log);
        var (provider, app) = Hosts.BuildApp(b => b
            .UseFeature(quick)
            .UseFeature(slow)
            .UseOptions(new KernelOptions { StopTimeout = TimeSpan.FromMilliseconds(50) }));
        using var _ = provider;

        await app.StartAsync(CancellationToken.None);
        await app.StopAsync(CancellationToken.None);

        Assert.Equal(KernelErrors.FeatureStopTimeout, app.Features.Single(static f => f.Manifest.Id == "xihan.test.slow").Error!.Code);
        Assert.Equal(LifecycleState.Stopped, app.Features.Single(static f => f.Manifest.Id == "xihan.test.quick").State);
    }

    [Fact]
    public async Task InvokeAsync_RunsPipelineAroundHandlerAndReturnsOutcome()
    {
        var log = new List<string>();
        var feature = new RecordingFeature(Manifests.Create("xihan.test.pipeline"), log)
        {
            OnConfigure = context =>
            {
                context.Services.AddSingleton(log);
                context.AddMiddleware<MiddlewareA>(PipelineStage.Authorize);
            },
        };
        var (provider, app) = Hosts.BuildApp(b => b.UseFeature(feature));
        using var _ = provider;

        // 未启动的应用不接收调用。
        var before = await Assert.ThrowsAsync<FrameworkException>(async () => await app.InvokeAsync(Hosts.Context(provider), static _ => ValueTask.CompletedTask));
        Assert.Equal(KernelErrors.InvalidAppState, before.Code);

        await app.StartAsync(CancellationToken.None);
        using var scope = provider.CreateScope();
        var result = await app.InvokeAsync(Hosts.Context(scope.ServiceProvider), context =>
        {
            log.Add("handler");
            context.SetOutcome(Error.Literal("ORDERS_NOTFOUND_001", "Not found."));
            return ValueTask.CompletedTask;
        });

        Assert.Equal("ORDERS_NOTFOUND_001", result.Error!.Code);
        Assert.Equal(["enter:A", "handler", "exit:A"], log.Where(static l => !l.StartsWith("configure", StringComparison.Ordinal) && !l.StartsWith("start", StringComparison.Ordinal)).ToList());
        Assert.Contains("observe      XiHan.Framework.Kernel.Diagnostics.ObserveMiddleware", app.DescribePipeline(), StringComparison.Ordinal);
        Assert.Contains("resilience   XiHan.Framework.Kernel.Pipeline.DeadlineMiddleware", app.DescribePipeline(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_BecomesUnexpectedOutcome()
    {
        var (provider, app) = Hosts.BuildApp(static b => b.UseFeature(new RecordingFeature(Manifests.Create("xihan.test.empty"), [])));
        using var _ = provider;
        await app.StartAsync(CancellationToken.None);

        var result = await app.InvokeAsync(Hosts.Context(provider), static _ => throw new InvalidOperationException("boom"));

        Assert.Equal(KernelErrors.Unexpected, result.Error!.Code);
    }

    [Fact]
    public void Build_MissingRequiredBinding_FailsAtBuildTime()
    {
        var feature = new RecordingFeature(Manifests.Create("xihan.persistence.inmemory", bindings: [new Binding("db", "connection:relational")]), []);

        var exception = Assert.Throws<FrameworkException>(() => Hosts.BuildApp(b => b.UseFeature(feature)));

        Assert.Equal(KernelErrors.MissingBinding, exception.Code);
    }

    [Fact]
    public void Build_BindingFromConfiguration_IsVisibleToFeature()
    {
        BindingValue? seen = null;
        var feature = new RecordingFeature(Manifests.Create("xihan.persistence.inmemory", bindings: [new Binding("db", "connection:relational")]), [])
        {
            OnConfigure = context => seen = context.GetBinding("db"),
        };
        var configuration = Hosts.Configuration(
            ("XiHan:Bindings:db:type", "connection:relational"),
            ("XiHan:Bindings:db:value", "Host=localhost"),
            ("XiHan:Bindings:db:schema", "public"));

        var (provider, _) = Hosts.BuildApp(b => b.UseFeature(feature), configuration);
        using var _ = provider;

        Assert.NotNull(seen);
        Assert.Equal("Host=localhost", seen.Value);
        Assert.Equal("public", seen.Property("schema").ValueOrThrow());
        Assert.False(seen.Property("missing").HasValue);
    }

    [Fact]
    public void Build_BindingTypeMismatch_Fails()
    {
        var feature = new RecordingFeature(Manifests.Create("xihan.persistence.inmemory", bindings: [new Binding("db", "connection:relational")]), []);
        var configuration = Hosts.Configuration(("XiHan:Bindings:db:type", "queue"), ("XiHan:Bindings:db:value", "amqp://"));

        var exception = Assert.Throws<FrameworkException>(() => Hosts.BuildApp(b => b.UseFeature(feature), configuration));

        Assert.Equal(KernelErrors.MissingBinding, exception.Code);
    }

    [Fact]
    public void Build_ProviderSelectionFromConfiguration_PicksProvider()
    {
        var memory = new RecordingFeature(Manifests.Create("xihan.caching.inmemory", provides: ["caching.local@1"]), []);
        var redis = new RecordingFeature(Manifests.Create("xihan.caching.redis", provides: ["caching.local@1"]), []);
        var consumer = new RecordingFeature(Manifests.Create("xihan.app.main", requires: ["caching.local@1"]), []);

        var (provider, app) = Hosts.BuildApp(b => b.UseFeature(consumer).UseFeature(memory).UseFeature(redis), Hosts.Configuration(("XiHan:Providers:caching.local", "xihan.caching.redis")));
        using var _ = provider;

        Assert.Equal("xihan.caching.redis", app.Graph.Providers["caching.local"].Manifest.Id);
    }

    [Fact]
    public void Build_WithoutBuiltInMiddleware_LeavesPipelineEmpty()
    {
        var (provider, app) = Hosts.BuildApp(static b => b.UseOptions(new KernelOptions { UseBuiltInMiddleware = false }));
        using var _ = provider;

        Assert.Equal(string.Empty, app.DescribePipeline());
        Assert.Equal(LifecycleState.Ready, app.State);
    }

    [Fact]
    public void Build_Twice_Throws()
    {
        var builder = new AppBuilder(new ServiceCollection(), Hosts.Configuration());
        builder.Build();

        Assert.Equal(KernelErrors.InvalidAppState, Assert.Throws<FrameworkException>(() => builder.Build()).Code);
        Assert.Equal(KernelErrors.InvalidAppState, Assert.Throws<FrameworkException>(() => builder.UseFeature(new RecordingFeature(Manifests.Create("xihan.test.late"), []))).Code);
    }

    [Fact]
    public async Task HealthContributor_DegradesTheApp_WithoutFailingIt()
    {
        var contributor = new StubContributor();
        var (provider, app) = Hosts.BuildApp(b =>
        {
            b.UseFeature(new RecordingFeature(Manifests.Create("xihan.app.main"), []));
            b.Services.AddSingleton<IHealthContributor>(contributor);
        });
        using var _ = provider;
        await app.StartAsync(CancellationToken.None);
        Assert.Equal(LifecycleState.Ready, app.State);

        // 条目失败只把应用压成 Degraded：仍就绪、仍存活、特性状态不变
        contributor.State = LifecycleState.Failed;
        Assert.Equal(LifecycleState.Degraded, app.State);
        var health = app.Health();
        Assert.Equal(LifecycleState.Degraded, health.State);
        Assert.True(health.Ready);
        Assert.True(health.Live);
        var contribution = Assert.Single(health.Contributions);
        Assert.Equal("plugins.demo", contribution.Name);
        Assert.Equal(LifecycleState.Failed, contribution.State);
        Assert.Equal(LifecycleState.Ready, Assert.Single(app.Features).State);

        contributor.State = LifecycleState.Ready;
        Assert.Equal(LifecycleState.Ready, app.State);
    }

    [Fact]
    public void Build_DuplicateFeatureManifest_Throws()
    {
        var first = new RecordingFeature(Manifests.Create("xihan.test.dup"), []);
        var second = new RecordingFeature(Manifests.Create("xihan.test.dup"), []);

        var exception = Assert.Throws<FrameworkException>(() => Hosts.BuildApp(b => b.UseFeature(first).UseFeature(second)));

        Assert.Equal(KernelErrors.DuplicateManifest, exception.Code);
    }

    /// <summary>
    /// 可改状态的健康贡献者。
    /// </summary>
    private sealed class StubContributor : IHealthContributor
    {
        /// <summary>
        /// 条目状态。
        /// </summary>
        public LifecycleState State { get; set; } = LifecycleState.Ready;

        /// <inheritdoc />
        public IReadOnlyList<ContributorStatus> Check() => [new ContributorStatus("plugins.demo", State)];
    }
}
