// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.SearchEngines.Documents;
using XiHan.Framework.SearchEngines.InMemory;
using XiHan.Framework.SearchEngines.Indexing;
using XiHan.Framework.SearchEngines.Querying;

namespace XiHan.Framework.SearchEngines.Tests;

/// <summary>
/// 搜索引擎契约的测试
/// </summary>
/// <remarks>
/// 断言只写在 <see cref="ISearchEngine"/> 契约上，不依赖任何具体实现的概念，
/// 因此新增实现包时可直接复用本套用例校验其行为一致性。
/// </remarks>
public class SearchEngineContractTests
{
    private const string Index = "articles";

    /// <summary>
    /// 创建索引后可查到存在
    /// </summary>
    [Fact]
    public async Task CreateIndex_ThenIndexExists()
    {
        var engine = new InMemorySearchEngine();

        Assert.False(await engine.IndexExistsAsync(Index, TestContext.Current.CancellationToken));
        Assert.True(await engine.CreateIndexAsync(BuildDefinition(), TestContext.Current.CancellationToken));
        Assert.True(await engine.IndexExistsAsync(Index, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 重复创建索引不报错且返回未创建
    /// </summary>
    [Fact]
    public async Task CreateIndex_WhenAlreadyExists_ReturnsFalse()
    {
        var engine = await CreateEngineWithIndexAsync();

        Assert.False(await engine.CreateIndexAsync(BuildDefinition(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 删除不存在的索引返回未删除
    /// </summary>
    [Fact]
    public async Task DeleteIndex_WhenMissing_ReturnsFalse()
    {
        var engine = new InMemorySearchEngine();

        Assert.False(await engine.DeleteIndexAsync(Index, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 向不存在的索引写入时抛出
    /// </summary>
    [Fact]
    public async Task Index_WhenIndexMissing_Throws()
    {
        var engine = new InMemorySearchEngine();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.IndexAsync(Index, new SearchDocument<Article>("1", NewArticle("1")), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 写入后可按标识取回
    /// </summary>
    [Fact]
    public async Task Index_ThenGetById()
    {
        var engine = await CreateEngineWithIndexAsync();
        await engine.IndexAsync(Index, new SearchDocument<Article>("1", NewArticle("1", title: "曦寒框架")), TestContext.Current.CancellationToken);

        var article = await engine.GetAsync<Article>(Index, "1", TestContext.Current.CancellationToken);

        Assert.NotNull(article);
        Assert.Equal("曦寒框架", article.Title);
    }

    /// <summary>
    /// 同标识重复写入整体覆盖
    /// </summary>
    [Fact]
    public async Task Index_WithSameId_Overwrites()
    {
        var engine = await CreateEngineWithIndexAsync();
        await engine.IndexAsync(Index, new SearchDocument<Article>("1", NewArticle("1", title: "旧标题")), TestContext.Current.CancellationToken);
        await engine.IndexAsync(Index, new SearchDocument<Article>("1", NewArticle("1", title: "新标题")), TestContext.Current.CancellationToken);

        var article = await engine.GetAsync<Article>(Index, "1", TestContext.Current.CancellationToken);

        Assert.Equal("新标题", article!.Title);
    }

    /// <summary>
    /// 删除后取不到
    /// </summary>
    [Fact]
    public async Task Delete_ThenGetReturnsNull()
    {
        var engine = await CreateEngineWithIndexAsync();
        await engine.IndexAsync(Index, new SearchDocument<Article>("1", NewArticle("1")), TestContext.Current.CancellationToken);

        Assert.True(await engine.DeleteAsync(Index, "1", TestContext.Current.CancellationToken));
        Assert.Null(await engine.GetAsync<Article>(Index, "1", TestContext.Current.CancellationToken));
        Assert.False(await engine.DeleteAsync(Index, "1", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 关键字命中可检索字段
    /// </summary>
    [Fact]
    public async Task Search_ByKeyword_MatchesSearchableFields()
    {
        var engine = await CreateSeededEngineAsync();

        var result = await engine.SearchAsync<Article>(
            new SearchRequest(Index) { Keyword = "分布式" },
            TestContext.Current.CancellationToken);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("2", result.Hits[0].Id);
    }

    /// <summary>
    /// 关键字大小写不敏感
    /// </summary>
    [Fact]
    public async Task Search_ByKeyword_IsCaseInsensitive()
    {
        var engine = await CreateSeededEngineAsync();

        var result = await engine.SearchAsync<Article>(
            new SearchRequest(Index) { Keyword = "XIHAN" },
            TestContext.Current.CancellationToken);

        Assert.Equal(1, result.TotalCount);
    }

    /// <summary>
    /// 无关键字时只按过滤条件检索
    /// </summary>
    [Fact]
    public async Task Search_WithoutKeyword_ReturnsAllMatchingFilters()
    {
        var engine = await CreateSeededEngineAsync();

        var result = await engine.SearchAsync<Article>(
            new SearchRequest(Index)
            {
                Filters = [new SearchFilter("category", SearchFilterOperator.Equal, "framework")]
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.TotalCount);
    }

    /// <summary>
    /// 过滤条件之间为与关系
    /// </summary>
    [Fact]
    public async Task Search_MultipleFilters_AreCombinedWithAnd()
    {
        var engine = await CreateSeededEngineAsync();

        var result = await engine.SearchAsync<Article>(
            new SearchRequest(Index)
            {
                Filters =
                [
                    new SearchFilter("category", SearchFilterOperator.Equal, "framework"),
                    new SearchFilter("views", SearchFilterOperator.GreaterThan, 100)
                ]
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("2", result.Hits[0].Id);
    }

    /// <summary>
    /// In 运算符匹配任一候选值
    /// </summary>
    [Fact]
    public async Task Search_WithInFilter_MatchesAnyValue()
    {
        var engine = await CreateSeededEngineAsync();

        var result = await engine.SearchAsync<Article>(
            new SearchRequest(Index)
            {
                Filters = [new SearchFilter("category", SearchFilterOperator.In, values: ["guide", "framework"])]
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(3, result.TotalCount);
    }

    /// <summary>
    /// 数值范围过滤按数值而非字符串比较
    /// </summary>
    [Fact]
    public async Task Search_WithRangeFilter_ComparesNumerically()
    {
        var engine = await CreateSeededEngineAsync();

        var result = await engine.SearchAsync<Article>(
            new SearchRequest(Index)
            {
                Filters = [new SearchFilter("views", SearchFilterOperator.GreaterThanOrEqual, 90)]
            },
            TestContext.Current.CancellationToken);

        // 90 与 1200 命中，9 不命中：按字符串比较会把 "9" 误判为大于 "90"
        Assert.Equal(2, result.TotalCount);
    }

    /// <summary>
    /// 按字段升序排序
    /// </summary>
    [Fact]
    public async Task Search_SortsByFieldAscending()
    {
        var engine = await CreateSeededEngineAsync();

        var result = await engine.SearchAsync<Article>(
            new SearchRequest(Index)
            {
                Sorts = [new SearchSort("views", SearchSortDirection.Ascending)]
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(["3", "1", "2"], result.Hits.Select(hit => hit.Id));
    }

    /// <summary>
    /// 分页返回指定区间且总数不受分页影响
    /// </summary>
    [Fact]
    public async Task Search_AppliesPaging()
    {
        var engine = await CreateSeededEngineAsync();

        var result = await engine.SearchAsync<Article>(
            new SearchRequest(Index)
            {
                Sorts = [new SearchSort("views", SearchSortDirection.Ascending)],
                Skip = 1,
                Take = 1
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(3, result.TotalCount);
        Assert.Single(result.Hits);
        Assert.Equal("1", result.Hits[0].Id);
    }

    /// <summary>
    /// 指定高亮字段时返回包裹标记的片段
    /// </summary>
    [Fact]
    public async Task Search_WithHighlight_ReturnsMarkedFragment()
    {
        var engine = await CreateSeededEngineAsync();

        var result = await engine.SearchAsync<Article>(
            new SearchRequest(Index)
            {
                Keyword = "分布式",
                HighlightFields = ["title"]
            },
            TestContext.Current.CancellationToken);

        Assert.Contains("<em>分布式</em>", result.Hits[0].Highlights["title"][0]);
    }

    /// <summary>
    /// 未指定高亮字段时不返回高亮
    /// </summary>
    [Fact]
    public async Task Search_WithoutHighlightFields_ReturnsNoHighlights()
    {
        var engine = await CreateSeededEngineAsync();

        var result = await engine.SearchAsync<Article>(
            new SearchRequest(Index) { Keyword = "分布式" },
            TestContext.Current.CancellationToken);

        Assert.Empty(result.Hits[0].Highlights);
    }

    /// <summary>
    /// 批量写入返回实际写入条数
    /// </summary>
    [Fact]
    public async Task IndexMany_ReturnsWrittenCount()
    {
        var engine = await CreateEngineWithIndexAsync();

        var count = await engine.IndexManyAsync(
            Index,
            [new SearchDocument<Article>("1", NewArticle("1")), new SearchDocument<Article>("2", NewArticle("2"))],
            TestContext.Current.CancellationToken);

        Assert.Equal(2, count);
    }

    /// <summary>
    /// 删除索引后其文档一并消失
    /// </summary>
    [Fact]
    public async Task DeleteIndex_DropsDocuments()
    {
        var engine = await CreateSeededEngineAsync();
        await engine.DeleteIndexAsync(Index, TestContext.Current.CancellationToken);
        await engine.CreateIndexAsync(BuildDefinition(), TestContext.Current.CancellationToken);

        var result = await engine.SearchAsync<Article>(new SearchRequest(Index), TestContext.Current.CancellationToken);

        Assert.Equal(0, result.TotalCount);
    }

    /// <summary>
    /// 构建索引定义
    /// </summary>
    /// <returns>索引定义</returns>
    private static SearchIndexDefinition BuildDefinition()
    {
        return new SearchIndexDefinition(Index,
        [
            new SearchFieldDefinition("title", SearchFieldType.Text, Searchable: true),
            new SearchFieldDefinition("category", SearchFieldType.Keyword),
            new SearchFieldDefinition("views", SearchFieldType.Integer, Sortable: true)
        ]);
    }

    /// <summary>
    /// 创建已建索引的引擎
    /// </summary>
    /// <returns>引擎</returns>
    private static async Task<InMemorySearchEngine> CreateEngineWithIndexAsync()
    {
        var engine = new InMemorySearchEngine();
        await engine.CreateIndexAsync(BuildDefinition(), TestContext.Current.CancellationToken);

        return engine;
    }

    /// <summary>
    /// 创建已写入样例文档的引擎
    /// </summary>
    /// <returns>引擎</returns>
    private static async Task<InMemorySearchEngine> CreateSeededEngineAsync()
    {
        var engine = await CreateEngineWithIndexAsync();
        await engine.IndexManyAsync(Index,
        [
            new SearchDocument<Article>("1", NewArticle("1", "XiHan 入门", "guide", 90)),
            new SearchDocument<Article>("2", NewArticle("2", "分布式事件总线", "framework", 1200)),
            new SearchDocument<Article>("3", NewArticle("3", "缓存抽象", "framework", 9))
        ], TestContext.Current.CancellationToken);

        return engine;
    }

    /// <summary>
    /// 构造样例文档
    /// </summary>
    /// <param name="id">标识</param>
    /// <param name="title">标题</param>
    /// <param name="category">分类</param>
    /// <param name="views">浏览量</param>
    /// <returns>文档</returns>
    private static Article NewArticle(string id, string title = "标题", string category = "guide", int views = 0)
    {
        return new Article { Id = id, Title = title, Category = category, Views = views };
    }
}

/// <summary>
/// 测试用文档
/// </summary>
public class Article
{
    /// <summary>
    /// 标识
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 分类
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 浏览量
    /// </summary>
    public int Views { get; set; }
}
