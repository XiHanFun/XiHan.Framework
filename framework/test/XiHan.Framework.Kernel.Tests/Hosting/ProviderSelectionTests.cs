// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Kernel.Tests.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Tests.Hosting;

/// <summary>
/// 提供者选择
/// </summary>
/// <remarks>
/// 显式 Provider 优先于 default，选中结果回填到节点，未被选中的节点不装配；被顶替的 IShadowAwareFeature 只登记与能力选择无关的服务
/// </remarks>
public sealed class ProviderSelectionTests
{
    /// <summary>
    /// 同时提供本地与分布式缓存的内存实现
    /// </summary>
    /// <remarks>
    /// 默认为 default 角色
    /// </remarks>
    private static Manifest Memory(ManifestRole role = ManifestRole.Default)
        => Manifests.Create("xihan.caching.inmemory", provides: ["caching.local@1", "caching.distributed@1"], requires: ["kernel.hosting@1"], role: role);

    /// <summary>
    /// 只提供分布式缓存的显式 Provider
    /// </summary>
    /// <remarks>
    /// 清单 ID 可改，便于构造第二个显式 Provider
    /// </remarks>
    private static Manifest Redis(string id = "xihan.caching.redis")
        => Manifests.Create(id, provides: ["caching.distributed@1"], requires: ["kernel.hosting@1"]);

    [Fact]
    public void Resolve_RecordsSelectedCapabilities_PerNode()
    {
        var app = Manifests.Create("xihan.app.main", requires: ["caching.local@1", "caching.distributed@1"]);

        var graph = CapabilityResolver.Resolve([KernelCapabilities.Manifest, Memory(), Redis(), app]);

        var memory = graph["xihan.caching.inmemory"];
        Assert.Equal(["caching.local"], memory.SelectedCapabilities);
        Assert.True(memory.IsSelectedFor("caching.local"));
        Assert.False(memory.IsSelectedFor("caching.distributed"));
        Assert.False(memory.IsShadowed);
        Assert.True(graph["xihan.caching.redis"].IsSelectedFor("caching.distributed"));
        Assert.Contains("caching.distributed@1 (not selected)", graph.Describe());
        Assert.True(graph[KernelCapabilities.Manifest.Id].IsSelectedFor(KernelCapabilities.Hosting));
    }

    [Fact]
    public void Resolve_MarksSelectionsRequested_OnlyWhenRequiredOrConfigured()
    {
        // 必需依赖与可选依赖都算被要求；没人依赖的能力是兜底选出
        var app = Manifests.Create("xihan.app.main", requires: ["caching.distributed@1", "caching.local@1?"]);
        var required = CapabilityResolver.Resolve([KernelCapabilities.Manifest, Memory(), Redis(), app]);
        Assert.True(required["xihan.caching.redis"].IsRequestedFor("caching.distributed"));
        Assert.True(required["xihan.caching.inmemory"].IsRequestedFor("caching.local"));
        Assert.False(required["xihan.caching.inmemory"].IsRequestedFor("caching.distributed"));

        var fallback = CapabilityResolver.Resolve([KernelCapabilities.Manifest, Memory(), Redis()]);
        Assert.True(fallback["xihan.caching.redis"].IsSelectedFor("caching.distributed"));
        Assert.False(fallback["xihan.caching.redis"].IsRequestedFor("caching.distributed"));
        Assert.True(fallback["xihan.caching.inmemory"].IsSelectedFor("caching.local"));
        Assert.False(fallback["xihan.caching.inmemory"].IsRequestedFor("caching.local"));
        // 兜底选出照样算选中：选中集合与图谱不变
        Assert.Equal(["caching.local"], fallback["xihan.caching.inmemory"].SelectedCapabilities);

        // 配置指定了提供者也是被要求的，即使没人依赖
        var configured = CapabilityResolver.Resolve([KernelCapabilities.Manifest, Memory(), Redis()], new Dictionary<string, string>(StringComparer.Ordinal) { ["caching.distributed"] = "xihan.caching.inmemory" });
        Assert.True(configured["xihan.caching.inmemory"].IsRequestedFor("caching.distributed"));
        Assert.False(configured["xihan.caching.inmemory"].IsRequestedFor("caching.local"));
        Assert.True(configured["xihan.caching.redis"].IsShadowed);
        Assert.False(configured["xihan.caching.redis"].IsRequestedFor("caching.distributed"));
    }

    [Fact]
    public void Build_UseProvider_MarksTheSelectionRequested()
    {
        var (provider, _) = Hosts.BuildApp(static b => b.UseFeature(new RecordingFeature(Memory(), [])).UseFeature(new RecordingFeature(Redis(), [])).UseProvider("caching.local", "xihan.caching.inmemory"));
        using var _ = provider;

        var graph = provider.GetRequiredService<ResolvedGraph>();
        Assert.True(graph["xihan.caching.inmemory"].IsRequestedFor("caching.local"));
        Assert.False(graph["xihan.caching.redis"].IsRequestedFor("caching.distributed"));
    }

    [Fact]
    public void Resolve_ExplicitProviderBeatsDefault_WhetherOrNotRequired()
    {
        var app = Manifests.Create("xihan.app.main", requires: ["caching.distributed@1"]);

        var required = CapabilityResolver.Resolve([KernelCapabilities.Manifest, Memory(), Redis(), app]);
        var unrequired = CapabilityResolver.Resolve([KernelCapabilities.Manifest, Memory(), Redis()]);

        Assert.Equal("xihan.caching.redis", required.Providers["caching.distributed"].Manifest.Id);
        Assert.Equal("xihan.caching.redis", unrequired.Providers["caching.distributed"].Manifest.Id);
        Assert.Equal("xihan.caching.inmemory", unrequired.Providers["caching.local"].Manifest.Id);
    }

    [Fact]
    public void Resolve_DefaultShadowedForAllCapabilities_IsMarkedShadowed()
    {
        var memory = Manifests.Create("xihan.persistence.inmemory", provides: ["persistence.relational@1"], requires: ["kernel.hosting@1"], role: ManifestRole.Default);
        var efcore = Manifests.Create("xihan.persistence.efcore", provides: ["persistence.relational@1"], requires: ["kernel.hosting@1"]);

        var graph = CapabilityResolver.Resolve([KernelCapabilities.Manifest, memory, efcore]);

        Assert.True(graph["xihan.persistence.inmemory"].IsShadowed);
        Assert.False(graph["xihan.persistence.efcore"].IsShadowed);
        Assert.Contains("xihan.persistence.inmemory 5.0.0 (default) (shadowed)", graph.Describe());

        // 配置可以反过来指定默认实现，显式 Provider 就被顶替
        var configured = CapabilityResolver.Resolve([KernelCapabilities.Manifest, memory, efcore], new Dictionary<string, string>(StringComparer.Ordinal) { ["persistence.relational"] = "xihan.persistence.inmemory" });
        Assert.True(configured["xihan.persistence.efcore"].IsShadowed);
        Assert.False(configured["xihan.persistence.inmemory"].IsShadowed);
    }

    [Fact]
    public void Resolve_TwoExplicitProviders_ThrowAmbiguous_EvenWhenNobodyRequiresThem()
    {
        var thrown = Assert.Throws<FrameworkException>(() => CapabilityResolver.Resolve([KernelCapabilities.Manifest, Memory(), Redis(), Redis("xihan.caching.garnet")]));

        // 候选列表只列显式 Provider，被顶替的 default 不应出现在报错里
        Assert.Equal(KernelErrors.AmbiguousProvider, thrown.Code);
        Assert.Contains("xihan.caching.redis", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("xihan.caching.garnet", thrown.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("xihan.caching.inmemory", thrown.Message, StringComparison.Ordinal);

        var unknown = Assert.Throws<FrameworkException>(() => CapabilityResolver.Resolve([KernelCapabilities.Manifest, Redis(), Redis("xihan.caching.garnet")], new Dictionary<string, string>(StringComparer.Ordinal) { ["caching.distributed"] = "xihan.caching.none" }));
        Assert.Equal(KernelErrors.MissingCapability, unknown.Code);
    }

    [Fact]
    public void Resolve_TwoDefaults_ThrowAmbiguous()
    {
        var thrown = Assert.Throws<FrameworkException>(() => CapabilityResolver.Resolve([KernelCapabilities.Manifest, Memory(), Manifests.Create("xihan.caching.other", provides: ["caching.local@1"], role: ManifestRole.Default)]));

        Assert.Equal(KernelErrors.AmbiguousProvider, thrown.Code);
    }

    [Fact]
    public async Task Build_SkipsShadowedFeatures_ConfigureAndStart()
    {
        var log = new List<string>();
        var memory = new RecordingFeature(Manifests.Create("xihan.persistence.inmemory", provides: ["persistence.relational@1"], requires: ["kernel.hosting@1"], role: ManifestRole.Default), log);
        var efcore = new RecordingFeature(Manifests.Create("xihan.persistence.efcore", provides: ["persistence.relational@1"], requires: ["kernel.hosting@1"]), log);
        var (provider, app) = Hosts.BuildApp(b => b.UseFeature(memory).UseFeature(efcore));
        using var _ = provider;

        await app.StartAsync(CancellationToken.None);
        await app.StopAsync(CancellationToken.None);

        Assert.Equal(["configure:xihan.persistence.efcore", "start:xihan.persistence.efcore", "stop:xihan.persistence.efcore"], log);
        Assert.DoesNotContain(app.Features, static f => f.Manifest.Id == "xihan.persistence.inmemory");
        Assert.True(app.Graph["xihan.persistence.inmemory"].IsShadowed);
    }

    [Fact]
    public async Task Build_ShadowAwareFeature_RegistersOnlyItsSelectionIndependentServices_WhenShadowed()
    {
        var log = new List<string>();
        // 被顶替的一方声明了必需绑定却没有提供：被顶替时不校验，不会因此启动失败
        var memory = new ShadowAwareFeature(Manifests.Create("xihan.persistence.inmemory", provides: ["persistence.relational@1"], requires: ["kernel.hosting@1"], role: ManifestRole.Default, bindings: [new Binding("db", "connection:relational")]), log)
        {
            OnShadowed = static context => context.Services.AddSingleton(new ShadowMarker(context.Node.IsShadowed)),
        };
        var efcore = new RecordingFeature(Manifests.Create("xihan.persistence.efcore", provides: ["persistence.relational@1"], requires: ["kernel.hosting@1"]), log);
        var (provider, app) = Hosts.BuildApp(b => b.UseFeature(memory).UseFeature(efcore));
        using var _ = provider;

        await app.StartAsync(CancellationToken.None);
        await app.StopAsync(CancellationToken.None);

        Assert.Equal(["shadowed:xihan.persistence.inmemory", "configure:xihan.persistence.efcore", "start:xihan.persistence.efcore", "stop:xihan.persistence.efcore"], log);
        Assert.True(provider.GetRequiredService<ShadowMarker>().Shadowed);
        Assert.DoesNotContain(app.Features, static f => f.Manifest.Id == "xihan.persistence.inmemory");

        // 没被顶替时照常 Configure，不调 ConfigureShadowed
        log.Clear();
        var (selected, selectedApp) = Hosts.BuildApp(b => b.UseFeature(new ShadowAwareFeature(Manifests.Create("xihan.persistence.inmemory", provides: ["persistence.relational@1"], requires: ["kernel.hosting@1"], role: ManifestRole.Default), log)));
        using var __ = selected;
        Assert.Equal(["configure:xihan.persistence.inmemory"], log);
        Assert.Contains(selectedApp.Features, static f => f.Manifest.Id == "xihan.persistence.inmemory");
    }

    /// <summary>
    /// 被顶替的特性登记的服务
    /// </summary>
    /// <param name="Shadowed">登记时节点是否标为被顶替</param>
    private sealed record ShadowMarker(bool Shadowed);
}
