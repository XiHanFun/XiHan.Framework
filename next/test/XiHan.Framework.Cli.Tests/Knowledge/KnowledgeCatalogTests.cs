// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using System.Text.Json;
using XiHan.Framework.Cli.Knowledge;
using XiHan.Framework.Cli.Packaging;
using XiHan.Framework.Kernel.Hosting;

namespace XiHan.Framework.Cli.Tests.Knowledge;

/// <summary>
/// 包与特性清单的索引与代码一致。
/// </summary>
/// <remarks>
/// 设环境变量 <see cref="CatalogBuilder.UpdateVariable"/>=1 运行时改写 <c>catalog.json</c>（<c>next/scripts/Update-CliKnowledge.ps1</c>）。
/// </remarks>
public sealed class KnowledgeCatalogTests
{
    [Fact]
    public void Catalog_MatchesTheWorkspaceAndEveryFeatureManifest()
    {
        var expected = JsonSerializer.Serialize(CatalogBuilder.Build(), KnowledgeJsonContext.Files.CatalogDocument) + "\n";
        if (Environment.GetEnvironmentVariable(CatalogBuilder.UpdateVariable) == "1")
        {
            File.WriteAllText(CatalogBuilder.CatalogPath, expected);
            return;
        }

        var actual = File.ReadAllText(CatalogBuilder.CatalogPath).Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.True(expected == actual, $"'{CatalogBuilder.CatalogPath}' is out of date; run next/scripts/Update-CliKnowledge.ps1.");
    }

    [Fact]
    public void EmbeddedCatalog_IsTheCommittedFile()
    {
        var knowledge = FrameworkKnowledge.Load();
        var committed = JsonSerializer.Deserialize(File.ReadAllText(CatalogBuilder.CatalogPath), KnowledgeJsonContext.Files.CatalogDocument)!;

        Assert.Equal(committed.Packages.Select(static p => p.Id), knowledge.Packages.Select(static p => p.Id));
        Assert.Equal(committed.Features.Select(static f => f.Id), knowledge.Features.Select(static f => f.Id));
    }

    [Fact]
    public void Catalog_CoversEveryPackageAndFeature()
    {
        var knowledge = FrameworkKnowledge.Load();
        var projects = Directory.GetDirectories(Path.Combine(TestWorkspace.NextRoot, "src")).Where(static d => File.Exists(Path.Combine(d, Path.GetFileName(d) + ".csproj"))).Select(Path.GetFileName).Order(StringComparer.Ordinal);

        Assert.Equal(projects, knowledge.Packages.Select(static p => p.Id));
        Assert.Contains(knowledge.Features, static f => f.Type == "XiHan.Framework.Messaging.Kafka.KafkaMessagingFeature" && f.Provides.SequenceEqual(["messaging.queue@1", "messaging.stream@1"]));
        Assert.Contains(knowledge.Features, static f => f.Type == "XiHan.Framework.Persistence.EntityFrameworkCore.EntityFrameworkCorePersistenceFeature<TContext>");
        // 排除名单上的特性必须真实存在，名单不会悄悄失效
        foreach (var excluded in CatalogBuilder.ExcludedFeatures)
        {
            Assert.NotNull(Type.GetType(excluded + ", " + excluded[..excluded.LastIndexOf('.')], throwOnError: false));
        }
    }

    [Fact]
    public void EveryFeatureBinding_IsAPackableBindingKind()
    {
        var types = FrameworkKnowledge.Load().Features.SelectMany(static f => f.Bindings).Select(static b => b.Type).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

        Assert.Equal(BindingKind.All.Select(static k => k.Type).Order(StringComparer.Ordinal), types);
    }

    [Fact]
    public void EveryPublicFeatureType_IsInTheCatalogOrExcluded()
    {
        var knowledge = FrameworkKnowledge.Load();
        var indexed = knowledge.Features.Select(static f => f.Type).ToHashSet(StringComparer.Ordinal);
        foreach (var package in knowledge.Packages.Where(static p => p.Id != "XiHan.Framework.Cli" && !p.Id.EndsWith(".Analyzers", StringComparison.Ordinal) && !p.Id.EndsWith(".Generators", StringComparison.Ordinal)))
        {
            foreach (var type in Assembly.Load(new AssemblyName(package.Id)).GetExportedTypes().Where(static t => !t.IsAbstract && !t.IsInterface && typeof(IFeature).IsAssignableFrom(t)))
            {
                var name = type.IsGenericTypeDefinition ? type.FullName![..type.FullName!.IndexOf('`', StringComparison.Ordinal)] : type.FullName!;
                Assert.True(indexed.Any(i => i == name || i.StartsWith(name + "<", StringComparison.Ordinal)) || CatalogBuilder.ExcludedFeatures.Contains(name), $"Feature '{name}' is missing from the catalog.");
            }
        }
    }
}
