// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Tests.Capabilities;

/// <summary>
/// <see cref="ManifestReader"/> 的行为。
/// </summary>
/// <remarks>
/// 读取全部字段、写出后可回读、各类非法清单都收敛为 <see cref="KernelErrors.InvalidManifest"/>。
/// </remarks>
public sealed class ManifestReaderTests
{
    /// <summary>
    /// 覆盖全部字段的合法清单样例。
    /// </summary>
    private const string Sample = """
        {
          "$schema": "https://spec.xihanfun.com/manifest/v1.json",
          "id": "xihan.caching.inmemory",
          "package": "XiHan.Framework.Caching.InMemory",
          "version": "5.0.0",
          "primitives": "1",
          "stability": "stable",
          "kind": "provider",
          "role": "default",
          "provides": [ { "id": "caching.local", "version": "1" } ],
          "requires": [ { "id": "kernel.hosting", "version": "^1" }, { "id": "multitenancy.context", "version": "1", "optional": true } ],
          "bindings": [ { "name": "cache", "type": "cache:distributed", "required": false } ],
          "contributes": {
            "pipeline": [ { "middleware": "XiHan.Framework.Caching.IdempotencyMiddleware", "stage": "idempotency" } ],
            "config": { "section": "XiHan:Caching", "schema": "schemas/caching.schema.json" },
            "health": [ "caching.local.capacity" ]
          },
          "activation": [ "onStartup" ],
          "entry": "XiHan.Framework.Caching.InMemory.InMemoryCachingFeature"
        }
        """;

    [Fact]
    public void Read_ParsesEveryField()
    {
        var manifest = ManifestReader.Read(Sample).ValueOrThrow();

        Assert.Equal("xihan.caching.inmemory", manifest.Id);
        Assert.Equal(ManifestKind.Provider, manifest.Kind);
        Assert.Equal(ManifestRole.Default, manifest.Role);
        Assert.Equal(Stability.Stable, manifest.Stability);
        Assert.Equal("caching.local@1", Assert.Single(manifest.Provides).ToString());
        Assert.Equal(["kernel.hosting@1", "multitenancy.context@1?"], manifest.Requires.Select(static r => r.ToString()));
        Assert.False(Assert.Single(manifest.Bindings).Required);
        Assert.Equal(PipelineStage.Idempotency, Assert.Single(manifest.Contributes.Pipeline).Stage);
        Assert.Equal("XiHan:Caching", manifest.Contributes.Config!.Section);
        Assert.Equal("XiHan.Framework.Caching.InMemory.InMemoryCachingFeature", manifest.Entry);
    }

    [Fact]
    public void Write_RoundTrips()
    {
        var original = ManifestReader.Read(Sample).ValueOrThrow();

        var json = ManifestReader.Write(original);
        var reparsed = ManifestReader.Read(json).ValueOrThrow();

        Assert.Equal(original.Id, reparsed.Id);
        Assert.Equal(original.Requires.Select(static r => r.ToString()), reparsed.Requires.Select(static r => r.ToString()));
        Assert.Equal(original.Contributes.Pipeline.Single().Stage, reparsed.Contributes.Pipeline.Single().Stage);
        Assert.Contains("\"$schema\"", json, StringComparison.Ordinal);
        Assert.Contains("\"isolation\"", ManifestReader.Write(original with { Kind = ManifestKind.Plugin, Isolation = PluginIsolation.OutOfProcess }), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("""{ "id": "xihan.a.b" }""")]
    [InlineData("""{ "id": "bad id", "version": "5.0.0", "primitives": "1", "stability": "stable", "kind": "capability" }""")]
    [InlineData("""{ "id": "xihan.a.b", "version": "5.0.0", "primitives": "0", "stability": "stable", "kind": "capability" }""")]
    [InlineData("""{ "id": "xihan.a.b", "version": "5.0.0", "primitives": "1", "stability": "solid", "kind": "capability" }""")]
    [InlineData("""{ "id": "xihan.a.b", "version": "5.0.0", "primitives": "1", "stability": "stable", "kind": "plugin" }""")]
    [InlineData("""{ "id": "xihan.a.b", "version": "5.0.0", "primitives": "1", "stability": "stable", "kind": "capability", "contributes": { "pipeline": [ { "middleware": "X", "stage": "nowhere" } ] } }""")]
    public void Read_InvalidManifest_ReturnsInvalidManifestError(string json)
    {
        var result = ManifestReader.Read(json);

        Assert.True(result.IsFailure);
        Assert.Equal(KernelErrors.InvalidManifest, result.Error!.Code);
    }

    [Fact]
    public void ReadFile_MissingFile_ReturnsError()
    {
        var result = ManifestReader.ReadFile(Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N") + ".json"));

        Assert.Equal(KernelErrors.InvalidManifest, result.Error!.Code);
    }
}
