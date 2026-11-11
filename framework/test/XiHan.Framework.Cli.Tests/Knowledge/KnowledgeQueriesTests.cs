// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Cli.Knowledge;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Tests.Knowledge;

/// <summary>
/// 三类查询：包与能力、配置节、文档
/// </summary>
public sealed class KnowledgeQueriesTests
{
    /// <summary>
    /// 知识库
    /// </summary>
    private static readonly Lazy<FrameworkKnowledge> _knowledge = new(FrameworkKnowledge.Load);

    /// <summary>
    /// 知识库
    /// </summary>
    private static FrameworkKnowledge Knowledge => _knowledge.Value;

    [Fact]
    public void Load_EmbedsDocsSpecConventionsAndReadmes()
    {
        var paths = Knowledge.Pages.Select(static p => p.Path).ToList();

        Assert.Contains("docs/deployment/pack.md", paths);
        Assert.Contains("framework/spec/rfcs/0016-packaging.md", paths);
        Assert.Contains("framework/spec/conventions/error-codes.md", paths);
        Assert.Contains("framework/src/XiHan.Framework.Messaging.Kafka/README.md", paths);
        Assert.DoesNotContain(paths, static p => p.Contains("node_modules", StringComparison.Ordinal) || p.Contains(".vitepress", StringComparison.Ordinal));
        var deployment = Knowledge.Pages.Single(static p => p.Path == "docs/deployment/pack.md");
        Assert.Equal("应用清单与 xihan pack", deployment.Title);
        Assert.False(string.IsNullOrEmpty(deployment.Summary));
    }

    [Fact]
    public void Info_WithoutName_ListsEveryPackageWithItsFeatures()
    {
        var response = KnowledgeQueries.Info(Knowledge, null).ValueOrThrow();

        Assert.Null(response.Packages);
        Assert.Equal(Knowledge.Packages.Count, response.Index!.Count);
        Assert.Contains(response.Index, static p => p.Id == "XiHan.Framework.Messaging.Kafka" && p.Features.SequenceEqual(["xihan.messaging.kafka"]));
    }

    [Theory]
    [InlineData("XiHan.Framework.Messaging.Kafka")]
    [InlineData("messaging.kafka")]
    [InlineData("messaging-kafka")]
    [InlineData("packages/messaging-kafka")]
    [InlineData("xihan.messaging.kafka")]
    [InlineData("KafkaMessagingFeature")]
    [InlineData("XiHan.Framework.Messaging.Kafka.KafkaMessagingFeature")]
    public void Info_FindsAPackageByAnyOfItsNames(string name)
    {
        var package = Assert.Single(KnowledgeQueries.Info(Knowledge, name).ValueOrThrow().Packages!);

        Assert.Equal("XiHan.Framework.Messaging.Kafka", package.Id);
        Assert.Equal("3", package.Ring);
        Assert.Equal($"dotnet add package XiHan.Framework.Messaging.Kafka --version {FrameworkInfo.Version}", package.Install);
        Assert.Contains(package.Dependencies, static d => d.Id == "Confluent.Kafka" && d.Version.Length > 0);
        var feature = Assert.Single(package.Features);
        Assert.Equal(["messaging.queue@1", "messaging.stream@1"], feature.Provides);
        Assert.Equal("stream", Assert.Single(feature.Bindings).Name);
        Assert.Equal(["messaging.kafka.connection"], feature.Health);
        Assert.Equal(["XiHan:Messaging:Kafka"], package.Sections);
        Assert.Equal(["docs/packages/messaging-kafka.md", "framework/src/XiHan.Framework.Messaging.Kafka/README.md"], package.Docs);
    }

    [Fact]
    public void Info_ByCapability_ListsProvidersAndConsumers()
    {
        var capability = KnowledgeQueries.Info(Knowledge, "messaging.queue@1").ValueOrThrow().Capability!;

        Assert.Equal("messaging.queue", capability.Id);
        Assert.Contains(capability.Providers, static p => p.Feature == "xihan.messaging.kafka" && p.Role == "provider" && p.Version == 1);
        Assert.Contains(capability.Providers, static p => p.Package == "XiHan.Framework.Messaging.InMemory" && p.Role == "default");
        Assert.NotEmpty(capability.RequiredBy);
    }

    [Fact]
    public void Info_UnknownName_IsNotFoundWithCandidates()
    {
        var error = KnowledgeQueries.Info(Knowledge, "Messaging.Kafkaa").Error!;

        Assert.Equal(CliErrors.KnowledgeNotFound, error.Code);
        Assert.Equal(CliMessages.KnowledgeNotFoundCandidates, error.MessageKey);
        Assert.Contains("XiHan.Framework.Messaging.Kafka", (string)error.Details!["candidates"]!, StringComparison.Ordinal);
    }

    [Fact]
    public void Config_WithoutQuery_ListsEverySection()
    {
        var response = KnowledgeQueries.Config(Knowledge, " ").ValueOrThrow();

        Assert.Equal(Knowledge.Sections.Count, response.Index!.Count);
        Assert.Contains(response.Index, static s => s.Section == "XiHan:Messaging:Kafka" && s.Package == "XiHan.Framework.Messaging.Kafka" && s.Keys == 6);
    }

    [Fact]
    public void Config_BySection_IncludesItsSubsections()
    {
        var sections = KnowledgeQueries.Config(Knowledge, "xihan:messaging").ValueOrThrow().Sections!.Select(static s => s.Section).ToList();

        Assert.Contains("XiHan:Messaging", sections);
        Assert.Contains("XiHan:Messaging:Kafka", sections);
        Assert.Contains("XiHan:Messaging:Outbox", sections);
        Assert.DoesNotContain("XiHan:MultiTenancy", sections);
    }

    [Fact]
    public void Config_ByKeyPath_ReturnsOnlyThatKey_AndMatchesPlaceholders()
    {
        var section = Assert.Single(KnowledgeQueries.Config(Knowledge, "XiHan:Messaging:Kafka:GroupId").ValueOrThrow().Sections!);
        var key = Assert.Single(section.Keys);
        Assert.Equal("XiHan:Messaging:Kafka:GroupId", key.Path);
        Assert.Equal("xihan", key.Default);

        var principal = Assert.Single(KnowledgeQueries.Config(Knowledge, "XiHan:Security:InMemory:Principals:alice:ApiKey").ValueOrThrow().Sections!);
        Assert.Equal("XiHan:Security:InMemory:Principals:{name}:ApiKey", Assert.Single(principal.Keys).Path);

        // 绝对键：登记在 InMemory 节下，路径不接在节后
        var tenant = Assert.Single(KnowledgeQueries.Config(Knowledge, "XiHan:MultiTenancy:Tenants:acme:IsEnabled").ValueOrThrow().Sections!);
        Assert.Equal("XiHan:MultiTenancy:InMemory", tenant.Section);
        Assert.Equal("XiHan:MultiTenancy:Tenants:{tenant}:IsEnabled", Assert.Single(tenant.Keys).Path);
    }

    [Fact]
    public void Config_ByPackageOrOptions_AndAppsettingsFromDefaults()
    {
        var byPackage = Assert.Single(KnowledgeQueries.Config(Knowledge, "Messaging.Kafka").ValueOrThrow().Sections!);
        var byOptions = Assert.Single(KnowledgeQueries.Config(Knowledge, "KafkaMessagingOptions").ValueOrThrow().Sections!);
        Assert.Equal(byPackage.Section, byOptions.Section);
        Assert.Equal("stream", byPackage.Binding);

        // 必填键取示例、有缺省的键取缺省，数值按 JSON 数值写；按名字展开的键不写
        Assert.Equal(
            """
            {
              "XiHan": {
                "Messaging": {
                  "Kafka": {
                    "BootstrapServers": "localhost:9092",
                    "GroupId": "xihan",
                    "TopicPrefix": "xihan.",
                    "HealthProbeIntervalSeconds": 15,
                    "RedeliveryDelaySeconds": 5
                  }
                }
              }
            }
            """.Replace("\r\n", "\n", StringComparison.Ordinal),
            byPackage.Appsettings);
    }

    [Fact]
    public void Config_PackageWithoutSections_AndUnknownText_AreNotFound()
    {
        var none = KnowledgeQueries.Config(Knowledge, "Primitives").Error!;
        Assert.Equal(CliErrors.KnowledgeNotFound, none.Code);
        Assert.Equal(CliMessages.KnowledgeNoSections, none.MessageKey);

        Assert.Equal(CliErrors.KnowledgeNotFound, KnowledgeQueries.Config(Knowledge, "NoSuchKeyAnywhere").Error!.Code);
    }

    [Fact]
    public void Search_ReturnsPagesContainingEveryTerm_RankedWithSnippets()
    {
        var results = KnowledgeQueries.Search(Knowledge, "KRaft Nacos", null, 5).ValueOrThrow().Results!;

        Assert.InRange(results.Count, 1, 5);
        Assert.Contains(results, static r => r.Path == "docs/deployment/pack.md");
        Assert.All(results, r =>
        {
            var page = Knowledge.Pages.Single(p => p.Path == r.Path);
            Assert.Contains("KRaft", page.Markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Nacos", page.Markdown, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrEmpty(r.Snippet));
        });
        Assert.Equal(results.OrderByDescending(static r => r.Score).Select(static r => r.Score), results.Select(static r => r.Score));
    }

    [Theory]
    [InlineData("deployment/pack", "docs/deployment/pack.md")]
    [InlineData("docs/deployment/pack.md", "docs/deployment/pack.md")]
    [InlineData("0016", "framework/spec/rfcs/0016-packaging.md")]
    [InlineData("0016-packaging", "framework/spec/rfcs/0016-packaging.md")]
    [InlineData("error-codes", "framework/spec/conventions/error-codes.md")]
    [InlineData("XiHan.Framework.Cli", "framework/src/XiHan.Framework.Cli/README.md")]
    public void Search_Page_ReadsAWholePage(string name, string path)
    {
        var page = KnowledgeQueries.Search(Knowledge, null, name, null).ValueOrThrow().Page!;

        Assert.Equal(path, page.Path);
        Assert.Equal(Knowledge.Pages.Single(p => p.Path == path).Markdown, page.Markdown);
    }

    [Fact]
    public void Search_InvalidInput_IsValidation_AndNoHitsIsNotFound()
    {
        Assert.Equal(CliErrors.KnowledgeSearchInvalid, KnowledgeQueries.Search(Knowledge, " ", null, null).Error!.Code);
        Assert.Equal(CliErrors.KnowledgeSearchInvalid, KnowledgeQueries.Search(Knowledge, "kafka", null, 0).Error!.Code);
        Assert.Equal(CliErrors.KnowledgeSearchInvalid, KnowledgeQueries.Search(Knowledge, "kafka", null, KnowledgeQueries.MaxLimit + 1).Error!.Code);
        Assert.Equal(CliErrors.KnowledgeNotFound, KnowledgeQueries.Search(Knowledge, "zzzz-no-such-term-zzzz", null, null).Error!.Code);
        Assert.Equal(CliErrors.KnowledgeNotFound, KnowledgeQueries.Search(Knowledge, null, "guide/no-such-page", null).Error!.Code);
    }

    [Fact]
    public void Schemas_LimitMatchesTheQueryMaximum()
    {
        Assert.Contains($"\"maximum\":{KnowledgeQueries.MaxLimit}", KnowledgeSchemas.Search.Request, StringComparison.Ordinal);
    }
}
