// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.SearchEngines.Abstractions.Results;

namespace XiHan.Framework.SearchEngines.Abstractions.Tests.Results;

/// <summary>
/// 检索结果的测试
/// </summary>
/// <remarks>
/// 命中总数与命中项数是两个独立的量：前者是全量命中数，后者受分页裁剪。
/// 用例刻意用「总数远大于命中项数」的组合锁住这一点，防止实现把 Hits.Count 当总数回填。
/// </remarks>
public class SearchResultTests
{
    /// <summary>
    /// 命中项与命中总数原样保留
    /// </summary>
    [Fact]
    public void Constructor_KeepsHitsAndTotalCount()
    {
        var hits = new[]
        {
            NewHit("1"),
            NewHit("2")
        };

        var result = new SearchResult<SearchTestDocument>(hits, 2);

        Assert.Equal(hits, result.Hits);
        Assert.Equal(2, result.TotalCount);
    }

    /// <summary>
    /// 命中总数不受分页裁剪影响
    /// </summary>
    [Fact]
    public void TotalCount_IsIndependentOfHitsCount()
    {
        var result = new SearchResult<SearchTestDocument>([NewHit("1")], 1000);

        Assert.Single(result.Hits);
        Assert.Equal(1000, result.TotalCount);
    }

    /// <summary>
    /// 命中总数用长整型承载，可超过整型上限
    /// </summary>
    /// <remarks>
    /// 大索引的命中总数会突破 int 上限，这里锁死类型宽度不被收窄。
    /// </remarks>
    [Fact]
    public void TotalCount_SupportsValuesBeyondInt32()
    {
        var result = new SearchResult<SearchTestDocument>([], 3_000_000_000L);

        Assert.Equal(3_000_000_000L, result.TotalCount);
    }

    /// <summary>
    /// 命中项顺序原样保留
    /// </summary>
    /// <remarks>
    /// 排序结果全靠这个顺序表达，实现不得重排。
    /// </remarks>
    [Fact]
    public void Hits_PreserveOrder()
    {
        var result = new SearchResult<SearchTestDocument>([NewHit("3"), NewHit("1"), NewHit("2")], 3);

        Assert.Equal(["3", "1", "2"], result.Hits.Select(hit => hit.Id));
    }

    /// <summary>
    /// 命中项为空引用时抛出空引用参数异常
    /// </summary>
    [Fact]
    public void Constructor_WhenHitsNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new SearchResult<SearchTestDocument>(null!, 0));

        Assert.Equal("hits", exception.ParamName);
    }

    /// <summary>
    /// 空结果没有命中项且总数为零
    /// </summary>
    [Fact]
    public void Empty_HasNoHitsAndZeroTotalCount()
    {
        var empty = SearchResult<SearchTestDocument>.Empty;

        Assert.Empty(empty.Hits);
        Assert.Equal(0, empty.TotalCount);
    }

    /// <summary>
    /// 空结果是按文档类型复用的单实例
    /// </summary>
    /// <remarks>
    /// 实现方在未命中时会反复返回它，必须是可安全共享的不可变对象。
    /// </remarks>
    [Fact]
    public void Empty_IsCachedSingleInstancePerDocumentType()
    {
        Assert.Same(SearchResult<SearchTestDocument>.Empty, SearchResult<SearchTestDocument>.Empty);
    }

    /// <summary>
    /// 构造命中项
    /// </summary>
    /// <param name="id">文档标识</param>
    /// <returns>命中项</returns>
    private static SearchHit<SearchTestDocument> NewHit(string id)
    {
        return new SearchHit<SearchTestDocument>(id, new SearchTestDocument(), 0d, new Dictionary<string, IReadOnlyList<string>>());
    }
}
