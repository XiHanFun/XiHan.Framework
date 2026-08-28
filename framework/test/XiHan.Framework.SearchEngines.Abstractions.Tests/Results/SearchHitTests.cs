// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.SearchEngines.Results;

namespace XiHan.Framework.SearchEngines.Abstractions.Tests.Results;

/// <summary>
/// 检索命中项的测试
/// </summary>
/// <remarks>
/// 命中项是实现回填给调用方的出参，四个分量都必须给全（高亮为空时给空字典而非空引用）。
/// 相等性只到引用一层，别指望用它比较两次检索的结果集。
/// </remarks>
public class SearchHitTests
{
    /// <summary>
    /// 四个分量原样保留
    /// </summary>
    [Fact]
    public void Constructor_KeepsAllComponents()
    {
        var document = new SearchTestDocument { Title = "分布式事件总线", Views = 1200 };
        var highlights = new Dictionary<string, IReadOnlyList<string>>
        {
            ["title"] = ["<em>分布式</em>事件总线"]
        };

        var hit = new SearchHit<SearchTestDocument>("2", document, 1.5d, highlights);

        Assert.Equal("2", hit.Id);
        Assert.Same(document, hit.Document);
        Assert.Equal(1.5d, hit.Score);
        Assert.Equal(["<em>分布式</em>事件总线"], hit.Highlights["title"]);
    }

    /// <summary>
    /// 未按相关度检索时得分为零且高亮为空字典
    /// </summary>
    [Fact]
    public void Constructor_WithoutScoreAndHighlights_UsesZeroAndEmptyDictionary()
    {
        var highlights = new Dictionary<string, IReadOnlyList<string>>();

        var hit = new SearchHit<SearchTestDocument>("1", new SearchTestDocument(), 0d, highlights);

        Assert.Equal(0d, hit.Score);
        Assert.Empty(hit.Highlights);
    }

    /// <summary>
    /// 一个字段可携带多个高亮片段
    /// </summary>
    [Fact]
    public void Highlights_SupportMultipleFragmentsPerField()
    {
        var highlights = new Dictionary<string, IReadOnlyList<string>>
        {
            ["title"] = ["<em>曦寒</em>", "框架<em>曦寒</em>"],
            ["summary"] = ["<em>曦寒</em>框架简介"]
        };

        var hit = new SearchHit<SearchTestDocument>("1", new SearchTestDocument(), 0.8d, highlights);

        Assert.Equal(2, hit.Highlights.Count);
        Assert.Equal(2, hit.Highlights["title"].Count);
        Assert.Single(hit.Highlights["summary"]);
    }

    /// <summary>
    /// 四个分量的引用全同时命中项相等
    /// </summary>
    [Fact]
    public void Equals_WithIdenticalComponents_IsTrue()
    {
        var document = new SearchTestDocument();
        var highlights = new Dictionary<string, IReadOnlyList<string>>();

        var left = new SearchHit<SearchTestDocument>("1", document, 1d, highlights);
        var right = new SearchHit<SearchTestDocument>("1", document, 1d, highlights);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    /// <summary>
    /// 高亮字典内容相同但实例不同时命中项不相等
    /// </summary>
    /// <remarks>
    /// 记录的值相等只逐字段调用默认比较器，字典与文档都没有值相等语义，
    /// 所以命中项的相等性实际停在引用一层。断言两次检索结果集相等要逐字段比。
    /// </remarks>
    [Fact]
    public void Equals_WithEquivalentButDistinctHighlightInstances_IsFalse()
    {
        var document = new SearchTestDocument();

        var left = new SearchHit<SearchTestDocument>("1", document, 1d, new Dictionary<string, IReadOnlyList<string>>());
        var right = new SearchHit<SearchTestDocument>("1", document, 1d, new Dictionary<string, IReadOnlyList<string>>());

        Assert.NotEqual(left, right);
    }

    /// <summary>
    /// 得分不同时命中项不相等
    /// </summary>
    [Fact]
    public void Equals_WhenScoreDiffers_IsFalse()
    {
        var document = new SearchTestDocument();
        var highlights = new Dictionary<string, IReadOnlyList<string>>();

        Assert.NotEqual(
            new SearchHit<SearchTestDocument>("1", document, 1d, highlights),
            new SearchHit<SearchTestDocument>("1", document, 2d, highlights));
    }

    /// <summary>
    /// with 表达式只改得分且不影响原对象
    /// </summary>
    [Fact]
    public void With_ChangesOnlyScore()
    {
        var document = new SearchTestDocument();
        var highlights = new Dictionary<string, IReadOnlyList<string>>();
        var original = new SearchHit<SearchTestDocument>("1", document, 1d, highlights);

        var boosted = original with { Score = 9d };

        Assert.Equal("1", boosted.Id);
        Assert.Same(document, boosted.Document);
        Assert.Same(highlights, boosted.Highlights);
        Assert.Equal(9d, boosted.Score);
        Assert.Equal(1d, original.Score);
    }

    /// <summary>
    /// 解构按声明顺序给出四个分量
    /// </summary>
    [Fact]
    public void Deconstruct_YieldsComponentsInDeclaredOrder()
    {
        var document = new SearchTestDocument();
        var highlights = new Dictionary<string, IReadOnlyList<string>>();

        var (id, hitDocument, score, hitHighlights) = new SearchHit<SearchTestDocument>("1", document, 1.25d, highlights);

        Assert.Equal("1", id);
        Assert.Same(document, hitDocument);
        Assert.Equal(1.25d, score);
        Assert.Same(highlights, hitHighlights);
    }
}
