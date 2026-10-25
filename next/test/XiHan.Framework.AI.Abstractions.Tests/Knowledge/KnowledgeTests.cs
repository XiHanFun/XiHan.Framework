// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.AI.Abstractions.Tests.Fixtures;
using XiHan.Framework.AI.Conformance.Embeddings;
using XiHan.Framework.Testing;

namespace XiHan.Framework.AI.Abstractions.Tests.Knowledge;

/// <summary>
/// 知识库流水线：切块、摄入与重摄入、检索与过滤、删除、维度不一致、没有 Provider、提示增强。
/// </summary>
public sealed class KnowledgeTests
{
    /// <summary>
    /// 基础配置：哈希嵌入 Provider，64 维，小块。
    /// </summary>
    private static readonly (string Key, string Value)[] _configuration =
    [
        ("XiHan:AI:Providers:hash:Kind", "hashing"),
        ("XiHan:AI:Providers:hash:EmbeddingDimensions", "64"),
        ("XiHan:AI:Knowledge:Dimensions", "64"),
        ("XiHan:AI:Knowledge:ChunkSize", "40"),
        ("XiHan:AI:Knowledge:ChunkOverlap", "10"),
        ("XiHan:AI:Knowledge:Top", "2"),
    ];

    [Fact]
    public void FixedWindow_ChunksWithOverlap_AndNormalizesNewlines()
    {
        var strategy = new FixedWindowChunkingStrategy();
        Assert.Empty(strategy.Chunk("   ", new ChunkingOptions()));
        Assert.Equal(["abcde", "defgh", "ghij"], strategy.Chunk("abcdefghij", new ChunkingOptions { MaxChunkSize = 5, Overlap = 2 }));
        Assert.Equal(["ab", "cd"], strategy.Chunk("ab\r\ncd", new ChunkingOptions { MaxChunkSize = 3, Overlap = 0 }).Select(static chunk => chunk.Trim()));
        Assert.Equal(["abc", "bcd", "cde"], strategy.Chunk("abcde", new ChunkingOptions { MaxChunkSize = 3, Overlap = 9 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChunkingOptions { MaxChunkSize = 0 });
    }

    [Fact]
    public async Task Ingest_Retrieve_Replace_AndRemove()
    {
        var store = new FakeKnowledgeStore();
        await using var host = await StartAsync(store);
        var ingestor = host.Services.GetRequiredService<IKnowledgeIngestor>();
        var retriever = host.Services.GetRequiredService<IKnowledgeRetriever>();

        var results = await ingestor.IngestAsync(
        [
            new KnowledgeIngestRequest { DocumentId = "cats", Text = "Cats purr when they are happy. Kittens chase string all day long.", Metadata = new Dictionary<string, string> { ["topic"] = "pets" } },
            new KnowledgeIngestRequest { DocumentId = "cars", Text = "Cars need fuel and regular maintenance to run well.", Metadata = new Dictionary<string, string> { ["topic"] = "vehicles" } },
            new KnowledgeIngestRequest { DocumentId = "", Text = "x" },
            new KnowledgeIngestRequest { DocumentId = "empty", Text = "   " },
        ], CancellationToken.None);
        // 66 与 51 个字符，窗口 40、步长 30 → 各 2 块
        Assert.Equal(2, results[0].ValueOrThrow());
        Assert.Equal(2, results[1].ValueOrThrow());
        Assert.Equal(AIErrors.RequestInvalid, results[2].Error?.Code);
        Assert.Equal(0, results[3].ValueOrThrow());
        Assert.Equal(4, store.Records(KnowledgeOptions.DefaultCollection).Count);
        Assert.Contains(store.Records(KnowledgeOptions.DefaultCollection), static record => record.Id == "cats#1" && record.Metadata["topic"] == "pets" && record.Vector.Length == 64);

        var found = (await retriever.RetrieveAsync(new KnowledgeQuery { Text = "cats purr happy" }, CancellationToken.None)).ValueOrThrow();
        Assert.Equal(2, found.Count);
        Assert.Equal("cats", found[0].DocumentId);
        Assert.True(found[0].Score >= found[1].Score);
        var filtered = (await retriever.RetrieveAsync(new KnowledgeQuery { Text = "cats purr happy", Top = 5, Filter = new RetrievalFilter { Metadata = new Dictionary<string, string> { ["topic"] = "vehicles" } } }, CancellationToken.None)).ValueOrThrow();
        Assert.All(filtered, chunk => Assert.Equal("cars", chunk.DocumentId));
        Assert.Empty((await retriever.RetrieveAsync(new KnowledgeQuery { Text = "cats purr happy", MinScore = 1.5 }, CancellationToken.None)).ValueOrThrow());
        Assert.Equal(AIErrors.RequestInvalid, (await retriever.RetrieveAsync(new KnowledgeQuery { Text = " " }, CancellationToken.None)).Error?.Code);

        // 重新摄入替换旧块
        Assert.Equal(1, (await ingestor.IngestAsync(new KnowledgeIngestRequest { DocumentId = "cats", Text = "short" }, CancellationToken.None)).ValueOrThrow());
        Assert.Equal(["cars#0", "cars#1", "cats#0"], store.Records(KnowledgeOptions.DefaultCollection).Select(static record => record.Id).Order(StringComparer.Ordinal));
        Assert.True((await ingestor.RemoveAsync(null, "cars", CancellationToken.None)).IsSuccess);
        Assert.Equal(["cats#0"], store.Records(KnowledgeOptions.DefaultCollection).Select(static record => record.Id));

        Assert.Equal(1, (await ingestor.IngestAsync(new KnowledgeIngestRequest { DocumentId = "other", Text = "elsewhere", Collection = "second" }, CancellationToken.None)).ValueOrThrow());
        Assert.Single(store.Records("second"));
        Assert.Equal(AIErrors.ProviderNotFound, (await ingestor.IngestAsync(new KnowledgeIngestRequest { DocumentId = "x", Text = "y", Provider = "none" }, CancellationToken.None)).Error?.Code);
    }

    [Fact]
    public async Task DimensionMismatch_AndMissingStore_AreReported()
    {
        await using var mismatched = await StartAsync(new FakeKnowledgeStore(), ("XiHan:AI:Knowledge:Dimensions", "32"));
        Assert.Equal(AIErrors.DimensionsMismatch, (await mismatched.Services.GetRequiredService<IKnowledgeIngestor>().IngestAsync(new KnowledgeIngestRequest { DocumentId = "d", Text = "text" }, CancellationToken.None)).Error?.Code);
        Assert.Equal(AIErrors.DimensionsMismatch, (await mismatched.Services.GetRequiredService<IKnowledgeRetriever>().RetrieveAsync(new KnowledgeQuery { Text = "text" }, CancellationToken.None)).Error?.Code);

        await using var noStore = await StartAsync(null);
        Assert.Equal(AIErrors.KnowledgeUnavailable, (await noStore.Services.GetRequiredService<IKnowledgeIngestor>().IngestAsync(new KnowledgeIngestRequest { DocumentId = "d", Text = "text" }, CancellationToken.None)).Error?.Code);
        Assert.Equal(AIErrors.KnowledgeUnavailable, (await noStore.Services.GetRequiredService<IKnowledgeIngestor>().RemoveAsync(null, "d", CancellationToken.None)).Error?.Code);
        Assert.Equal(AIErrors.KnowledgeUnavailable, (await noStore.Services.GetRequiredService<IKnowledgeRetriever>().RetrieveAsync(new KnowledgeQuery { Text = "text" }, CancellationToken.None)).Error?.Code);
    }

    [Fact]
    public async Task Augmenter_UsesBuiltInTemplate_OrConfiguredPrompt()
    {
        await using var host = await StartAsync(new FakeKnowledgeStore(), ("XiHan:AI:Knowledge:PromptName", "rag"), ("XiHan:AI:Prompts:rag:Template", "CTX:{context}|Q:{question}"));
        var augmenter = host.Services.GetRequiredService<IRagPromptAugmenter>();
        var chunks = new[] { new RetrievedChunk { Id = "a#0", DocumentId = "a", ChunkIndex = 0, Text = "first", Score = 0.9 }, new RetrievedChunk { Id = "b#1", DocumentId = "b", ChunkIndex = 1, Text = "second", Score = 0.5 } };
        var configured = augmenter.Augment("why?", chunks);
        Assert.StartsWith("CTX:[1] (document: a, chunk: 0)\nfirst\n\n[2] (document: b, chunk: 1)\nsecond\n\n|Q:why?", configured, StringComparison.Ordinal);
        Assert.Equal("why?", augmenter.Augment("why?", []));

        await using var plain = await StartAsync(new FakeKnowledgeStore());
        var builtIn = plain.Services.GetRequiredService<IRagPromptAugmenter>().Augment("why?", chunks);
        Assert.Contains("# Knowledge snippets\n[1] (document: a, chunk: 0)\nfirst", builtIn, StringComparison.Ordinal);
        Assert.EndsWith("# Question\nwhy?", builtIn, StringComparison.Ordinal);
    }

    /// <summary>
    /// 启动：哈希嵌入 Provider，可选的知识库存储与额外配置。
    /// </summary>
    private static Task<TestHost> StartAsync(FakeKnowledgeStore? store, params (string Key, string Value)[] extra)
        => TestHost.StartAsync(
            static b => b.UseFeature<HashingEmbeddingFeature>(),
            _configuration.Concat(extra).GroupBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(static group => new KeyValuePair<string, string?>(group.Key, group.Last().Value)),
            services =>
            {
                if (store is not null)
                {
                    services.AddSingleton<IKnowledgeStore>(store);
                }
            });
}
