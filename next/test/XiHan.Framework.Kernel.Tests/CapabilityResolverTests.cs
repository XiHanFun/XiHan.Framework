// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Tests;

public sealed class CapabilityResolverTests
{
    [Fact]
    public void Resolve_OrdersDependenciesBeforeDependents()
    {
        var app = Manifests.Create("xihan.app.main", requires: ["caching.local@1", "persistence.relational@1"]);
        var caching = Manifests.Create("xihan.caching.inmemory", provides: ["caching.local@1"], requires: ["kernel.hosting@1"]);
        var persistence = Manifests.Create("xihan.persistence.inmemory", provides: ["persistence.relational@1"], requires: ["caching.local@1"]);

        var graph = CapabilityResolver.Resolve([KernelCapabilities.Manifest, app, persistence, caching]);

        var order = graph.Nodes.Select(static n => n.Manifest.Id).ToList();
        Assert.Equal("xihan.kernel.core", order[0]);
        Assert.True(order.IndexOf("xihan.caching.inmemory") < order.IndexOf("xihan.persistence.inmemory"));
        Assert.True(order.IndexOf("xihan.persistence.inmemory") < order.IndexOf("xihan.app.main"));
        Assert.Same(graph["xihan.caching.inmemory"], graph.Providers["caching.local"]);
        Assert.Contains("requires caching.local@1 -> xihan.caching.inmemory", graph.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_MissingRequired_ThrowsMissingCapability()
    {
        var app = Manifests.Create("xihan.app.main", requires: ["caching.local@1"]);

        var exception = Assert.Throws<FrameworkException>(() => CapabilityResolver.Resolve([KernelCapabilities.Manifest, app]));

        Assert.Equal(KernelErrors.MissingCapability, exception.Code);
        Assert.Contains("xihan.app.main 需要 caching.local@1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_VersionMismatch_ThrowsVersionConflictNamingBothSides()
    {
        var app = Manifests.Create("xihan.app.main", requires: ["caching.local@1"]);
        var caching = Manifests.Create("xihan.caching.redis", provides: ["caching.local@2"]);

        var exception = Assert.Throws<FrameworkException>(() => CapabilityResolver.Resolve([KernelCapabilities.Manifest, app, caching]));

        Assert.Equal(KernelErrors.CapabilityVersionConflict, exception.Code);
        Assert.Contains("xihan.caching.redis 提供 caching.local@2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_MultipleCandidates_RequiresDefaultRoleOrSelection()
    {
        var app = Manifests.Create("xihan.app.main", requires: ["caching.local@1"]);
        var memory = Manifests.Create("xihan.caching.inmemory", provides: ["caching.local@1"]);
        var redis = Manifests.Create("xihan.caching.redis", provides: ["caching.local@1"]);

        var ambiguous = Assert.Throws<FrameworkException>(() => CapabilityResolver.Resolve([KernelCapabilities.Manifest, app, memory, redis]));
        Assert.Equal(KernelErrors.AmbiguousProvider, ambiguous.Code);

        var withDefault = CapabilityResolver.Resolve([KernelCapabilities.Manifest, app, memory with { Role = ManifestRole.Default }, redis]);
        Assert.Equal("xihan.caching.inmemory", withDefault.Providers["caching.local"].Manifest.Id);

        var selected = CapabilityResolver.Resolve([KernelCapabilities.Manifest, app, memory, redis], new Dictionary<string, string> { ["caching.local"] = "xihan.caching.redis" });
        Assert.Equal("xihan.caching.redis", selected.Providers["caching.local"].Manifest.Id);

        var wrongSelection = Assert.Throws<FrameworkException>(() => CapabilityResolver.Resolve([KernelCapabilities.Manifest, app, memory, redis], new Dictionary<string, string> { ["caching.local"] = "xihan.caching.none" }));
        Assert.Equal(KernelErrors.MissingCapability, wrongSelection.Code);
    }

    [Fact]
    public void Resolve_OptionalMissing_RecordsDegradation()
    {
        var app = Manifests.Create("xihan.app.main", requires: ["multitenancy.context@1?", "kernel.hosting@1"]);

        var graph = CapabilityResolver.Resolve([KernelCapabilities.Manifest, app]);

        var node = graph["xihan.app.main"];
        Assert.True(node.IsDegraded);
        Assert.Equal("multitenancy.context@1?", Assert.Single(node.MissingOptional).ToString());
        Assert.Contains("(missing, optional)", graph.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_Cycle_ThrowsCircularWithPath()
    {
        var a = Manifests.Create("xihan.test.a", provides: ["test.a@1"], requires: ["test.b@1"]);
        var b = Manifests.Create("xihan.test.b", provides: ["test.b@1"], requires: ["test.a@1"]);

        var exception = Assert.Throws<FrameworkException>(() => CapabilityResolver.Resolve([a, b]));

        Assert.Equal(KernelErrors.CircularCapability, exception.Code);
        Assert.Contains("xihan.test.a -> xihan.test.b -> xihan.test.a", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_DuplicateManifestId_Throws()
    {
        var a = Manifests.Create("xihan.test.a");

        var exception = Assert.Throws<FrameworkException>(() => CapabilityResolver.Resolve([a, a with { Version = "5.0.1" }]));

        Assert.Equal(KernelErrors.DuplicateManifest, exception.Code);
    }
}
