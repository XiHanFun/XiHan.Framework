// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.SearchEngines.Abstractions.Querying;

namespace XiHan.Framework.SearchEngines.Abstractions.Tests.Querying;

/// <summary>
/// 排序项的测试
/// </summary>
/// <remarks>
/// 相关度字段名是抽象与实现之间的哨兵值：实现看到它就翻译成按打分排序，看到别的就当普通字段。
/// 这个字面量一旦改动，所有实现的分支判断会同时失效，故单独锁死。
/// </remarks>
public class SearchSortTests
{
    /// <summary>
    /// 相关度字段名固定为下划线 score
    /// </summary>
    [Fact]
    public void ScoreField_IsUnderscoreScore()
    {
        Assert.Equal("_score", SearchSort.ScoreField);
    }

    /// <summary>
    /// 未指定方向时为升序
    /// </summary>
    [Fact]
    public void Constructor_WithoutDirection_DefaultsToAscending()
    {
        var sort = new SearchSort("views");

        Assert.Equal("views", sort.Field);
        Assert.Equal(SearchSortDirection.Ascending, sort.Direction);
    }

    /// <summary>
    /// 指定的方向原样保留
    /// </summary>
    [Fact]
    public void Constructor_WithDirection_KeepsIt()
    {
        var sort = new SearchSort("views", SearchSortDirection.Descending);

        Assert.Equal(SearchSortDirection.Descending, sort.Direction);
    }

    /// <summary>
    /// 相关度排序项按相关度字段降序
    /// </summary>
    /// <remarks>
    /// 降序是唯一有意义的方向：相关度升序等于把最不相关的排在最前。
    /// </remarks>
    [Fact]
    public void ByScore_IsScoreFieldDescending()
    {
        Assert.Equal(SearchSort.ScoreField, SearchSort.ByScore.Field);
        Assert.Equal(SearchSortDirection.Descending, SearchSort.ByScore.Direction);
    }

    /// <summary>
    /// 相关度排序项是复用的单实例
    /// </summary>
    [Fact]
    public void ByScore_IsCachedSingleInstance()
    {
        Assert.Same(SearchSort.ByScore, SearchSort.ByScore);
    }

    /// <summary>
    /// 相关度排序项与等价的手写排序项值相等
    /// </summary>
    [Fact]
    public void Equals_ComparesFieldAndDirection()
    {
        Assert.Equal(new SearchSort(SearchSort.ScoreField, SearchSortDirection.Descending), SearchSort.ByScore);
        Assert.NotEqual(new SearchSort(SearchSort.ScoreField), SearchSort.ByScore);
        Assert.NotEqual(new SearchSort("views", SearchSortDirection.Descending), SearchSort.ByScore);
    }

    /// <summary>
    /// 相同字段与方向的排序项哈希一致
    /// </summary>
    [Fact]
    public void GetHashCode_IsConsistentWithEquals()
    {
        var left = new SearchSort("views", SearchSortDirection.Descending);
        var right = new SearchSort("views", SearchSortDirection.Descending);

        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    /// <summary>
    /// with 表达式只改方向且不影响原对象
    /// </summary>
    [Fact]
    public void With_ChangesOnlyDirection()
    {
        var ascending = SearchSort.ByScore with { Direction = SearchSortDirection.Ascending };

        Assert.Equal(SearchSort.ScoreField, ascending.Field);
        Assert.Equal(SearchSortDirection.Ascending, ascending.Direction);
        Assert.Equal(SearchSortDirection.Descending, SearchSort.ByScore.Direction);
    }

    /// <summary>
    /// 解构按声明顺序给出字段与方向
    /// </summary>
    [Fact]
    public void Deconstruct_YieldsFieldAndDirection()
    {
        var (field, direction) = new SearchSort("views", SearchSortDirection.Descending);

        Assert.Equal("views", field);
        Assert.Equal(SearchSortDirection.Descending, direction);
    }
}
