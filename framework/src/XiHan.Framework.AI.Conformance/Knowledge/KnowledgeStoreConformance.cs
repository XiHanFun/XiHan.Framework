// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.AI.Conformance.Embeddings;
using XiHan.Framework.Primitives;
using Xunit;

namespace XiHan.Framework.AI.Conformance.Knowledge;

/// <summary>
/// 知识库存储的一致性套件（RFC-0022 §知识库）
/// </summary>
/// <remarks>
/// 写入与按相似度排序、最低得分、文档与元数据过滤、重写入替换、按文档删除、按块序号批量裁剪、集合隔离、空集合搜索。Provider 继承并实现 <see cref="StartAsync"/>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class KnowledgeStoreConformance
{
    /// <summary>
    /// 启动装配了被测存储的夹具
    /// </summary>
    protected abstract Task<KnowledgeStoreHost> StartAsync();

    /// <summary>
    /// 用例 1：写入后按向量搜索，按得分降序，最多 top 条
    /// </summary>
    /// <remarks>
    /// 空集合返回空
    /// </remarks>
    [Fact]
    public async Task Search_OrdersBySimilarity_AndHonoursTop()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var generator = new HashingEmbeddingGenerator(host.Dimensions);
        var collection = Collection("order");
        Assert.Empty((await host.Store.SearchAsync(collection, new KnowledgeSearch(generator.Embed("anything"), 5), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());

        var records = new[]
        {
            Record(generator, "cats", 0, "cats purr and chase mice"),
            Record(generator, "dogs", 0, "dogs bark and fetch sticks"),
            Record(generator, "cats", 1, "kittens are young cats that purr"),
            Record(generator, "cars", 0, "cars drive on roads"),
        };
        Assert.True((await host.Store.UpsertAsync(collection, records, CancellationToken.None).ConfigureAwait(false)).IsSuccess);

        var found = (await host.Store.SearchAsync(collection, new KnowledgeSearch(generator.Embed("cats purr"), 3), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(3, found.Count);
        Assert.Equal("cats#0", found[0].Id);
        Assert.Equal("cats#1", found[1].Id);
        Assert.True(found[0].Score >= found[1].Score && found[1].Score >= found[2].Score);
        Assert.Equal(("cats", 0, "cats purr and chase mice"), (found[0].DocumentId, found[0].ChunkIndex, found[0].Text));
        Assert.Single((await host.Store.SearchAsync(collection, new KnowledgeSearch(generator.Embed("cats purr"), 1), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
    }

    /// <summary>
    /// 用例 2：按文档与元数据过滤，元数据原样取回
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

        var byDocument = (await host.Store.SearchAsync(collection, new KnowledgeSearch(vector, 10) { Filter = new RetrievalFilter { DocumentIds = ["b", "c"] } }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(["b", "c"], byDocument.Select(static chunk => chunk.DocumentId).Order(StringComparer.Ordinal));

        var byMetadata = (await host.Store.SearchAsync(collection, new KnowledgeSearch(vector, 10) { Filter = new RetrievalFilter { Metadata = new Dictionary<string, string> { ["lang"] = "en", ["tier"] = "gold" } } }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        var only = Assert.Single(byMetadata);
        Assert.Equal("a", only.DocumentId);
        Assert.Equal("gold", only.Metadata["tier"]);

        var both = (await host.Store.SearchAsync(collection, new KnowledgeSearch(vector, 10) { Filter = new RetrievalFilter { DocumentIds = ["a", "b"], Metadata = new Dictionary<string, string> { ["tier"] = "silver" } } }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal("b", Assert.Single(both).DocumentId);
        Assert.Empty((await host.Store.SearchAsync(collection, new KnowledgeSearch(vector, 10) { Filter = new RetrievalFilter { Metadata = new Dictionary<string, string> { ["lang"] = "fr" } } }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
    }

    /// <summary>
    /// 用例 3：同 ID 重写入替换，按文档删除清掉它的全部块且不影响别的文档与集合
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
        var all = (await host.Store.SearchAsync(first, new KnowledgeSearch(generator.Embed("text"), 10), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(3, all.Count);
        Assert.Equal("new text", all.Single(static chunk => chunk.Id == "doc#0").Text);

        Assert.True((await host.Store.DeleteAsync(first, ["doc", "missing"], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        var remaining = (await host.Store.SearchAsync(first, new KnowledgeSearch(generator.Embed("text"), 10), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal("keep", Assert.Single(remaining).DocumentId);
        Assert.Single((await host.Store.SearchAsync(second, new KnowledgeSearch(generator.Embed("other collection"), 10), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
        Assert.True((await host.Store.DeleteAsync(Collection("never"), ["doc"], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
    }

    /// <summary>
    /// 用例 4：裁剪只删一篇文档里块序号不小于给定值的记录
    /// </summary>
    /// <remarks>
    /// 更小序号的块与别的文档不动；没有多余的块、文档或集合不存在都成功；给 0 删掉整篇
    /// </remarks>
    [Fact]
    public async Task Trim_RemovesOnlyChunksAtOrBeyondTheCount()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var generator = new HashingEmbeddingGenerator(host.Dimensions);
        var collection = Collection("trim");
        var records = new[]
        {
            Record(generator, "doc", 0, "first part"),
            Record(generator, "doc", 1, "second part"),
            Record(generator, "doc", 2, "third part"),
            Record(generator, "other", 3, "other part"),
        };
        Assert.True((await host.Store.UpsertAsync(collection, records, CancellationToken.None).ConfigureAwait(false)).IsSuccess);

        Assert.True((await host.Store.TrimAsync(collection, [new KnowledgeTrim("doc", 1)], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.Equal(["doc#0", "other#3"], await IdsAsync(host, collection, generator).ConfigureAwait(false));

        Assert.True((await host.Store.TrimAsync(collection, [new KnowledgeTrim("doc", 5)], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.True((await host.Store.TrimAsync(collection, [new KnowledgeTrim("missing", 0)], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.True((await host.Store.TrimAsync(Collection("never"), [new KnowledgeTrim("doc", 0)], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.Equal(["doc#0", "other#3"], await IdsAsync(host, collection, generator).ConfigureAwait(false));

        Assert.True((await host.Store.TrimAsync(collection, [new KnowledgeTrim("doc", 0)], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.Equal(["other#3"], await IdsAsync(host, collection, generator).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 5：一次裁剪多篇文档，空集合不报错
    /// </summary>
    /// <remarks>
    /// 每篇按自己的块数裁剪，不在集合里的文档不动
    /// </remarks>
    [Fact]
    public async Task Trim_HandlesSeveralDocumentsInOneCall()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var generator = new HashingEmbeddingGenerator(host.Dimensions);
        var collection = Collection("trim-many");
        var records = new[]
        {
            Record(generator, "a", 0, "a first part"),
            Record(generator, "a", 1, "a second part"),
            Record(generator, "b", 0, "b first part"),
            Record(generator, "b", 1, "b second part"),
            Record(generator, "c", 0, "c first part"),
            Record(generator, "c", 1, "c second part"),
        };
        Assert.True((await host.Store.UpsertAsync(collection, records, CancellationToken.None).ConfigureAwait(false)).IsSuccess);

        Assert.True((await host.Store.TrimAsync(collection, [], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.True((await host.Store.TrimAsync(collection, [new KnowledgeTrim("a", 1), new KnowledgeTrim("b", 0), new KnowledgeTrim("missing", 0)], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.Equal(["a#0", "c#0", "c#1"], await IdsAsync(host, collection, generator).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 6：最低得分由存储施加
    /// </summary>
    /// <remarks>
    /// 得分低于它的块不返回；比最高得分还高时结果为空
    /// </remarks>
    [Fact]
    public async Task Search_SkipsChunksBelowMinScore()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var generator = new HashingEmbeddingGenerator(host.Dimensions);
        var collection = Collection("min-score");
        var records = new[]
        {
            Record(generator, "cats", 0, "cats purr and chase mice"),
            Record(generator, "dogs", 0, "dogs bark and fetch sticks"),
            Record(generator, "cars", 0, "cars drive on roads"),
        };
        Assert.True((await host.Store.UpsertAsync(collection, records, CancellationToken.None).ConfigureAwait(false)).IsSuccess);

        var vector = generator.Embed("cats purr and chase mice");
        var all = (await host.Store.SearchAsync(collection, new KnowledgeSearch(vector, 10), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(3, all.Count);

        // 门槛取第一与第二名之间：只剩完全相同的那块
        var threshold = (all[0].Score + all[1].Score) / 2;
        var above = (await host.Store.SearchAsync(collection, new KnowledgeSearch(vector, 10) { MinScore = threshold }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal("cats#0", Assert.Single(above).Id);
        Assert.Empty((await host.Store.SearchAsync(collection, new KnowledgeSearch(vector, 10) { MinScore = all[0].Score + 1 }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
    }

    /// <summary>
    /// 集合里全部记录的 ID（按序数排序）
    /// </summary>
    private static async Task<List<string>> IdsAsync(KnowledgeStoreHost host, string collection, HashingEmbeddingGenerator generator)
        => (await host.Store.SearchAsync(collection, new KnowledgeSearch(generator.Embed("part"), 100), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static chunk => chunk.Id).Order(StringComparer.Ordinal).ToList();

    /// <summary>
    /// 每次用例一个不重名的集合
    /// </summary>
    protected static string Collection(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// 一条记录
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
