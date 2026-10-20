// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Primitives.Tests;

public sealed class ManifestTests
{
    private sealed record RequirementCase(string Input, string Id, int Version, bool Optional);

    private sealed record RequirementVectors(RequirementCase[] Valid, string[] Invalid);

    private static Manifest CreateProvider() => new()
    {
        Id = "xihan.caching.inmemory",
        Package = "XiHan.Framework.Caching.InMemory",
        Version = "5.0.0-preview.1",
        Primitives = 1,
        Stability = Stability.Stable,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Default,
        Provides = [new CapabilityDeclaration("caching.local", 1)],
        Requires = [CapabilityRequirement.Parse("kernel.hosting@^1")],
        Contributes = new Contributions
        {
            Pipeline = [new PipelineContribution("XiHan.Framework.Caching.IdempotencyMiddleware", PipelineStage.Idempotency)],
            Config = new ConfigContribution("XiHan:Caching", "schemas/caching.schema.json"),
            Health = ["caching.local.capacity"],
        },
        Entry = "XiHan.Framework.Caching.InMemory.InMemoryCachingFeature",
    };

    [Fact]
    public void CapabilityRequirement_Parse_MatchesConformanceVectors()
    {
        var vectors = ConformanceVectors.Load<RequirementVectors>("capability-requirements.json");

        foreach (var expected in vectors.Valid)
        {
            var requirement = CapabilityRequirement.Parse(expected.Input);
            Assert.Equal(expected.Id, requirement.Id);
            Assert.Equal(expected.Version, requirement.Version);
            Assert.Equal(expected.Optional, requirement.Optional);
        }

        Assert.All(vectors.Invalid, static text => Assert.False(CapabilityRequirement.TryParse(text, out _), text));
    }

    [Fact]
    public void CapabilityDeclaration_ParsesAndFormats()
    {
        var declaration = CapabilityDeclaration.Parse("persistence.relational@2");

        Assert.Equal("persistence.relational", declaration.Id);
        Assert.Equal(2, declaration.Version);
        Assert.Equal("persistence.relational@2", declaration.ToString());
        Assert.False(CapabilityDeclaration.TryParse("persistence.relational", out _));
        Assert.False(CapabilityDeclaration.TryParse("persistence.relational@0", out _));
        Assert.True(CapabilityRequirement.Parse("persistence.relational@2").IsSatisfiedBy(declaration));
        Assert.False(CapabilityRequirement.Parse("persistence.relational@1").IsSatisfiedBy(declaration));
    }

    [Fact]
    public void CapabilityId_AndManifestId_ValidateFormat()
    {
        Assert.True(CapabilityId.IsValid("persistence.relational"));
        Assert.False(CapabilityId.IsValid("Persistence.Relational"));
        Assert.Equal("persistence", CapabilityId.DomainOf("persistence.relational"));
        Assert.True(ManifestId.IsValid("xihan.persistence.efcore"));
        Assert.False(ManifestId.IsValid("persistence.efcore"));
    }

    [Fact]
    public void Manifest_Validate_AcceptsWellFormedProvider()
    {
        var manifest = CreateProvider();

        Assert.True(manifest.Validate().IsSuccess);
        Assert.Equal(["onStartup"], manifest.Activation);
    }

    [Fact]
    public void Manifest_RejectsIllFormedFields()
    {
        Assert.Throws<ArgumentException>(() => CreateProvider() with { Id = "caching.inmemory" });
        Assert.Throws<ArgumentException>(() => CreateProvider() with { Version = "5" });
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateProvider() with { Primitives = 0 });
        Assert.Throws<ArgumentException>(() => CreateProvider() with { Activation = ["onBoot"] });
        Assert.Throws<ArgumentException>(() => CreateProvider() with { Permissions = ["root"] });
        Assert.Throws<ArgumentException>(() => new Binding("Db", "connection:relational"));
        Assert.Throws<ArgumentException>(() => new ConfigContribution("Caching"));
    }

    [Fact]
    public void Manifest_Validate_EnforcesCrossFieldRules()
    {
        var pluginWithoutIsolation = CreateProvider() with { Kind = ManifestKind.Plugin };
        var providerWithPermissions = CreateProvider() with { Permissions = ["net.outbound:api.example.com"] };
        var duplicateProvides = CreateProvider() with { Provides = [new CapabilityDeclaration("caching.local", 1), new CapabilityDeclaration("caching.local", 2)] };
        var plugin = CreateProvider() with
        {
            Id = "xihan.plugins.sample",
            Kind = ManifestKind.Plugin,
            Isolation = PluginIsolation.OutOfProcess,
            Permissions = ["capability:caching.distributed", "fs.read:./data"],
            Activation = ["onOperation:orders.*", "onEvent:orders.created"],
        };

        Assert.True(pluginWithoutIsolation.Validate().IsFailure);
        Assert.True(providerWithPermissions.Validate().IsFailure);
        Assert.True(duplicateProvides.Validate().IsFailure);
        Assert.True(plugin.Validate().IsSuccess);
    }
}
