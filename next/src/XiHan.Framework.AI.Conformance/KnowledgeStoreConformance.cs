// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.Primitives;
using Xunit;

namespace XiHan.Framework.AI.Conformance;

/// <summary>
/// 知识库存储的一致性套件（RFC-0022 §知识库）：写入与按相似度排序、文档与元数据过滤、重写入替换、按文档删除、集合隔离、空集合搜索。Provider 继承并实现 <see cref="StartAsync"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class KnowledgeStoreConformance
{
    /// <summary>
    /// 启动装配了被测存储的夹具。
    /// </summary>
    protected abstract Task<KnowledgeStoreHost> StartAsync();

    /// <summary>
    /// 用例 1：写入后按向量搜索，按得分降序，最多 top 条；空集合返回空。
    /// </summary>
    [Fact]
    public async Task Search_OrdersBySimilarity_AndHonoursTop()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var generator = new HashingEmbeddingGenerator(host.Dimensions);
        var collection = Collection("order");
        Assert.Empty((await host.Store.SearchAsync(collection, generator.Embed("anything"), 5, null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());

        var records = new[]
        {
            Record(generator, "cats", 0, "cats purr and chase mice"),
            Record(generator, "dogs", 0, "dogs bark and fetch sticks"),
            Record(generator, "cats", 1, "kittens are young cats that purr"),
            Record(generator, "cars", 0, "cars drive on roads"),
        };
        Assert.True((await host.Store.UpsertAsync(collection, records, CancellationToken.None).ConfigureAwait(false)).IsSuccess);

        var found = (await host.Store.SearchAsync(collection, generator.Embed("cats purr"), 3, null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(3, found.Count);
        Assert.Equal("cats#0", found[0].Id);
        Assert.Equal("cats#1", found[1].Id);
        Assert.True(found[0].Score >= found[1].Score && found[1].Score >= found[2].Score);
        Assert.Equal(("cats", 0, "cats purr and chase mice"), (found[0].DocumentId, found[0].ChunkIndex, found[0].Text));
        Assert.Single((await host.Store.SearchAsync(collection, generator.Embed("cats purr"), 1, null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
    }

    /// <summary>
    /// 用例 2：按文档与元数据过滤，元数据原样取回。
    /// </summary>
    [Fact]
    public async Task Search_FiltersByDocumentsAndMetadata()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var generator = new HashingEmbeddingGenerator(host.Dimensions);
        var collection = Collection("filter");
        var records = new[]
        {
            Record(generator, "a", 0, "shared words here", ("lang", "en"), ("tier", "gold")),
            Record(generator, "b", 0, "shared words here", ("lang", "en"), ("tier", "silver")),
            Record(generator, "c", 0, "shared words here", ("lang", "zh"), ("tier", "gold")),
        };
        Assert.True((await host.Store.UpsertAsync(collection, records, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        var vector = generator.Embed("shared words here");

        var byDocument = (await host.Store.SearchAsync(collection, vector, 10, new RetrievalFilter { DocumentIds = ["b", "c"] }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(["b", "c"], byDocument.Select(static chunk => chunk.DocumentId).Order(StringComparer.Ordinal));

        var byMetadata = (await host.Store.SearchAsync(collection, vector, 10, new RetrievalFilter { Metadata = new Dictionary<string, string> { ["lang"] = "en", ["tier"] = "gold" } }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        var only = Assert.Single(byMetadata);
        Assert.Equal("a", only.DocumentId);
        Assert.Equal("gold", only.Metadata["tier"]);

        var both = (await host.Store.SearchAsync(collection, vector, 10, new RetrievalFilter { DocumentIds = ["a", "b"], Metadata = new Dictionary<string, string> { ["tier"] = "silver" } }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal("b", Assert.Single(both).DocumentId);
        Assert.Empty((await host.Store.SearchAsync(collection, vector, 10, new RetrievalFilter { Metadata = new Dictionary<string, string> { ["lang"] = "fr" } }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
    }

    /// <summary>
    /// 用例 3：同 ID 重写入替换，按文档删除清掉它的全部块且不影响别的文档与集合。
    /// </summary>
    [Fact]
    public async Task Upsert_Replaces_AndDeleteRemovesWholeDocuments_PerCollection()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var generator = new HashingEmbeddingGenerator(host.Dimensions);
        var first = Collection("one");
        var second = Collection("two");
        Assert.True((await host.Store.UpsertAsync(first, [Record(generator, "doc", 0, "old text"), Record(generator, "doc", 1, "second chunk"), Record(generator, "keep", 0, "keep me")], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.True((await host.Store.UpsertAsync(second, [Record(generator, "doc", 0, "other collection")], CancellationToken.None).ConfigureAwait(false)).IsSuccess);

        Assert.True((await host.Store.UpsertAsync(first, [Record(generator, "doc", 0, "new text")], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        var all = (await host.Store.SearchAsync(first, generator.Embed("text"), 10, null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(3, all.Count);
        Assert.Equal("new text", all.Single(static chunk => chunk.Id == "doc#0").Text);

        Assert.True((await host.Store.DeleteAsync(first, ["doc", "missing"], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        var remaining = (await host.Store.SearchAsync(first, generator.Embed("text"), 10, null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal("keep", Assert.Single(remaining).DocumentId);
        Assert.Single((await host.Store.SearchAsync(second, generator.Embed("other collection"), 10, null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
        Assert.True((await host.Store.DeleteAsync(Collection("never"), ["doc"], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
    }

    /// <summary>
    /// 每次用例一个不重名的集合。
    /// </summary>
    protected static string Collection(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// 一条记录。
    /// </summary>
    protected static KnowledgeRecord Record(HashingEmbeddingGenerator generator, string documentId, int index, string text, params (string Key, string Value)[] metadata)
    {
        ArgumentNullException.ThrowIfNull(generator);
        return new KnowledgeRecord
        {
            Id = KnowledgeRecord.IdOf(documentId, index),
            DocumentId = documentId,
            ChunkIndex = index,
            Text = text,
            Metadata = metadata.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal),
            Vector = generator.Embed(text),
        };
    }
}
