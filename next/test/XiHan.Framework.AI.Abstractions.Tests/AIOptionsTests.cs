// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.AI.Abstractions.Providers;

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// 选项读取与校验
/// </summary>
/// <remarks>
/// 值不合法即抛出，不按缺省处理
/// </remarks>
public sealed class AIOptionsTests
{
    [Fact]
    public void FromConfiguration_ReadsEverySection()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XiHan:AI:DefaultProvider"] = "cloud",
            ["XiHan:AI:DefaultEmbeddingProvider"] = "local",
            ["XiHan:AI:Providers:cloud:Kind"] = "OpenAI",
            ["XiHan:AI:Providers:cloud:Endpoint"] = "https://api.example.com/v1",
            ["XiHan:AI:Providers:cloud:ApiKey"] = "sk-test",
            ["XiHan:AI:Providers:cloud:ChatModel"] = "gpt",
            ["XiHan:AI:Providers:cloud:EmbeddingModel"] = "emb",
            ["XiHan:AI:Providers:cloud:EmbeddingDimensions"] = "256",
            ["XiHan:AI:Providers:cloud:TimeoutSeconds"] = "12.5",
            ["XiHan:AI:Providers:cloud:Properties:organization"] = "org-1",
            ["XiHan:AI:Providers:local:Kind"] = "openai",
            ["XiHan:AI:Providers:local:Endpoint"] = "",
            ["XiHan:AI:Providers:local:ChatModel"] = "qwen",
            ["XiHan:AI:Tools:orders.create"] = "place_order",
            ["XiHan:AI:Guardrails:Enabled"] = "true",
            ["XiHan:AI:Guardrails:BlockedKeywords:0"] = "a",
            ["XiHan:AI:Guardrails:BlockedKeywords:1"] = "b",
            ["XiHan:AI:Guardrails:UseBuiltInInjectionHeuristics"] = "false",
            ["XiHan:AI:Knowledge:Collection"] = "docs",
            ["XiHan:AI:Knowledge:Dimensions"] = "128",
            ["XiHan:AI:Knowledge:ChunkSize"] = "500",
            ["XiHan:AI:Knowledge:ChunkOverlap"] = "50",
            ["XiHan:AI:Knowledge:Top"] = "3",
            ["XiHan:AI:Knowledge:EmbeddingProvider"] = "local",
            ["XiHan:AI:Knowledge:PromptName"] = "rag",
        }).Build();

        var options = AIOptions.FromConfiguration(configuration);
        Assert.Equal(("cloud", "local"), (options.ResolveDefaultProvider(), options.ResolveDefaultEmbeddingProvider()));
        var cloud = options.Providers["cloud"];
        Assert.Equal(("cloud", "openai", "https://api.example.com/v1", "sk-test", "gpt", "emb", 256, TimeSpan.FromSeconds(12.5), "org-1"), (cloud.Name, cloud.Kind, cloud.Endpoint!.ToString(), cloud.ApiKey, cloud.ChatModel, cloud.EmbeddingModel, cloud.EmbeddingDimensions, cloud.Timeout, cloud.Properties["organization"]));
        var local = options.Providers["local"];
        Assert.Equal(("openai", null, AIProviderOptions.DefaultTimeout), (local.Kind, local.Endpoint, local.Timeout));
        Assert.Equal("place_order", options.Tools["orders.create"]);
        Assert.Equal((true, 2, false), (options.Guardrails.Enabled, options.Guardrails.BlockedKeywords.Count, options.Guardrails.UseBuiltInInjectionHeuristics));
        Assert.Equal(("docs", 128, 500, 50, 3, "local", "rag"), (options.Knowledge.Collection, options.Knowledge.Dimensions, options.Knowledge.ChunkSize, options.Knowledge.ChunkOverlap, options.Knowledge.Top, options.Knowledge.EmbeddingProvider, options.Knowledge.PromptName));
        Assert.Equal((500, 50), (options.Knowledge.Chunking.MaxChunkSize, options.Knowledge.Chunking.Overlap));
    }

    [Fact]
    public void Defaults_AndValidation()
    {
        var empty = AIOptions.FromConfiguration(new ConfigurationBuilder().Build());
        Assert.Null(empty.ResolveDefaultProvider());
        Assert.Equal((KnowledgeOptions.DefaultCollection, 1536, 1000, 100, 5), (empty.Knowledge.Collection, empty.Knowledge.Dimensions, empty.Knowledge.ChunkSize, empty.Knowledge.ChunkOverlap, empty.Knowledge.Top));
        Assert.False(empty.Guardrails.Enabled);
        Assert.True(empty.Guardrails.UseBuiltInInjectionHeuristics);

        var single = new AIOptions { Providers = new Dictionary<string, AIProviderOptions> { ["only"] = new("only", "openai") } };
        Assert.Equal(("only", "only"), (single.ResolveDefaultProvider(), single.ResolveDefaultEmbeddingProvider()));

        Assert.ThrowsAny<ArgumentException>(() => new AIProviderOptions("Bad Name", "openai"));
        Assert.ThrowsAny<ArgumentException>(() => new AIProviderOptions("ok", " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AIProviderOptions("ok", "openai") { EmbeddingDimensions = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new AIProviderOptions("ok", "openai") { Timeout = TimeSpan.Zero });
        Assert.Throws<ArgumentOutOfRangeException>(() => new KnowledgeOptions { Dimensions = 0 });
        Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOptions { Collection = "Bad Name" });
        Assert.Throws<ArgumentOutOfRangeException>(() => new KnowledgeQuery { Text = "x", Top = 0 });
        Assert.Throws<ArgumentException>(() => new AIProviderOptions("ok", "openai") { Endpoint = new Uri("ftp://example.com/v1") });
        Assert.Equal("http://localhost:11434/v1", new AIProviderOptions("ok", "openai") { Endpoint = new Uri("http://localhost:11434/v1") }.Endpoint!.ToString());
    }

    [Theory]
    [InlineData("XiHan:AI:Providers:cloud:Kind", "", "XiHan:AI:Providers:cloud:Kind")]
    [InlineData("XiHan:AI:Providers:cloud:Endpoint", "api.example.com/v1", "XiHan:AI:Providers:cloud:Endpoint")]
    [InlineData("XiHan:AI:Providers:cloud:Endpoint", "localhost:11434/v1", "XiHan:AI:Providers:cloud:Endpoint")]
    [InlineData("XiHan:AI:Providers:cloud:EmbeddingDimensions", "large", "XiHan:AI:Providers:cloud:EmbeddingDimensions")]
    [InlineData("XiHan:AI:Providers:cloud:EmbeddingDimensions", "0", "XiHan:AI:Providers:cloud:EmbeddingDimensions")]
    [InlineData("XiHan:AI:Providers:cloud:TimeoutSeconds", "5m", "XiHan:AI:Providers:cloud:TimeoutSeconds")]
    [InlineData("XiHan:AI:Providers:cloud:TimeoutSeconds", "-1", "XiHan:AI:Providers:cloud:TimeoutSeconds")]
    [InlineData("XiHan:AI:Guardrails:Enabled", "yes", "XiHan:AI:Guardrails:Enabled")]
    [InlineData("XiHan:AI:Guardrails:UseBuiltInInjectionHeuristics", "off", "XiHan:AI:Guardrails:UseBuiltInInjectionHeuristics")]
    [InlineData("XiHan:AI:Knowledge:Dimensions", "1,536", "XiHan:AI:Knowledge:Dimensions")]
    [InlineData("XiHan:AI:Knowledge:ChunkSize", "0", "XiHan:AI:Knowledge:ChunkSize")]
    [InlineData("XiHan:AI:Knowledge:ChunkOverlap", "-5", "XiHan:AI:Knowledge:ChunkOverlap")]
    [InlineData("XiHan:AI:Knowledge:Top", "three", "XiHan:AI:Knowledge:Top")]
    [InlineData("XiHan:AI:Prompts:rag:Description", "no template", "XiHan:AI:Prompts:rag:Template")]
    public void InvalidValues_Throw_InsteadOfFallingBackToDefaults(string key, string value, string reported)
    {
        var settings = new Dictionary<string, string?>
        {
            ["XiHan:AI:Providers:cloud:Kind"] = "openai",
            ["XiHan:AI:Providers:cloud:ApiKey"] = "sk-test",
            [key] = value,
        };

        var thrown = Assert.ThrowsAny<ArgumentException>(() => AIOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(settings).Build()));
        Assert.Contains(reported, thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingKind_Throws()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["XiHan:AI:Providers:local:ChatModel"] = "qwen" }).Build();
        Assert.Contains("XiHan:AI:Providers:local:Kind", Assert.ThrowsAny<ArgumentException>(() => AIOptions.FromConfiguration(configuration)).Message, StringComparison.Ordinal);
    }
}
