// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.AI.Abstractions.Tests.Fixtures;
using XiHan.Framework.AI.Conformance.Embeddings;
using XiHan.Framework.Testing;

namespace XiHan.Framework.AI.Abstractions.Tests.Knowledge;

/// <summary>
/// 文档 ID 不含孤立的代理项
/// </summary>
/// <remarks>
/// RFC-0022「知识库」（ADR-0321）：向量键是记录 ID 的 UTF-8 的 SHA-256，只差在孤立代理项上的两篇文档宽松编码之后写进同一组键、互相覆盖分块，删除与过滤也落到另一篇上。摄入、删除与文档过滤都拒收
/// </remarks>
public sealed class KnowledgeDocumentIdTests
{
    /// <summary>
    /// 哈希嵌入 Provider，64 维
    /// </summary>
    private static readonly KeyValuePair<string, string?>[] _configuration =
    [
        new("XiHan:AI:Providers:hash:Kind", "hashing"),
        new("XiHan:AI:Providers:hash:EmbeddingDimensions", "64"),
        new("XiHan:AI:Knowledge:Dimensions", "64"),
    ];

    [Fact]
    public async Task DocumentsThatDifferOnlyInALoneSurrogate_AreRejected_WithoutWritingAChunk()
    {
        var store = new FakeKnowledgeStore();
        await using var host = await StartAsync(store);
        var ingestor = host.Services.CreateScope().ServiceProvider.GetRequiredService<IKnowledgeIngestor>();

        // 改之前两篇都写进去：向量存储里它们的块键相同，后一篇覆盖前一篇
        var results = await ingestor.IngestAsync(
        [
            new KnowledgeIngestRequest { DocumentId = "doc\uD800", Text = "first document" },
            new KnowledgeIngestRequest { DocumentId = "doc\uDBFF", Text = "second document" },
            new KnowledgeIngestRequest { DocumentId = "\uDE00\uD83D", Text = "reversed pair" },
            new KnowledgeIngestRequest { DocumentId = "doc😀", Text = "a valid pair" },
        ], CancellationToken.None);

        Assert.All(results.Take(3), static result => Assert.Equal((AIErrors.RequestInvalid, AIMessages.RequestInvalid), (result.Error?.Code, result.Error?.MessageKey)));
        Assert.Equal("the document id has a lone surrogate", results[0].Error?.Details?["reason"]);
        Assert.Equal(1, results[3].ValueOrThrow());
        Assert.Equal(["doc😀#0"], store.Records(KnowledgeOptions.DefaultCollection).Select(static record => record.Id));
    }

    [Fact]
    public async Task RemovingADocumentIdWithALoneSurrogate_FailsTheBatch_AndLeavesTheStoreAlone()
    {
        var store = new FakeKnowledgeStore();
        await using var host = await StartAsync(store);
        var ingestor = host.Services.CreateScope().ServiceProvider.GetRequiredService<IKnowledgeIngestor>();
        Assert.Equal(1, (await ingestor.IngestAsync(new KnowledgeIngestRequest { DocumentId = "doc�", Text = "kept" }, CancellationToken.None)).ValueOrThrow());

        // 宽松编码之后 doc\uD800 就是 doc�：改之前删掉的是这一篇
        var removed = await ingestor.RemoveAsync(null, ["other", "doc\uD800"], CancellationToken.None);

        Assert.Equal((AIErrors.RequestInvalid, AIMessages.RequestInvalid), (removed.Error?.Code, removed.Error?.MessageKey));
        Assert.Equal("a document id has a lone surrogate", removed.Error?.Details?["reason"]);
        Assert.Equal(["doc�#0"], store.Records(KnowledgeOptions.DefaultCollection).Select(static record => record.Id));
    }

    [Fact]
    public void DocumentFilters_RejectLoneSurrogates_AtConstruction()
    {
        Assert.Throws<ArgumentException>(static () => new RetrievalFilter { DocumentIds = ["a", "doc\uDC00"] });
        Assert.Equal(["doc😀"], new RetrievalFilter { DocumentIds = ["doc😀"] }.DocumentIds);
    }

    /// <summary>
    /// 启动：哈希嵌入 Provider 与给定的知识库存储
    /// </summary>
    private static Task<TestHost> StartAsync(FakeKnowledgeStore store)
        => TestHost.StartAsync(static b => b.UseFeature<HashingEmbeddingFeature>(), _configuration, services => services.AddSingleton<IKnowledgeStore>(store));
}
