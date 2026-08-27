// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.SearchEngines.Querying;

namespace XiHan.Framework.SearchEngines.Abstractions.Tests.Querying;

/// <summary>
/// 排序方向的测试
/// </summary>
/// <remarks>
/// 序号被锁死是因为默认值有实际语义：<see cref="SearchSort"/> 的方向参数默认取 0，
/// 若把 Descending 挪到 0 位，所有省略方向的排序项会静默倒转。
/// </remarks>
public class SearchSortDirectionTests
{
    /// <summary>
    /// 各成员的序号不漂移
    /// </summary>
    /// <param name="direction">排序方向</param>
    /// <param name="expected">期望序号</param>
    [Theory]
    [InlineData(SearchSortDirection.Ascending, 0)]
    [InlineData(SearchSortDirection.Descending, 1)]
    public void Value_IsStable(SearchSortDirection direction, int expected)
    {
        Assert.Equal(expected, (int)direction);
    }

    /// <summary>
    /// 成员集合与顺序不漂移
    /// </summary>
    [Fact]
    public void Members_AreAscendingAndDescending()
    {
        Assert.Equal(
            new[] { SearchSortDirection.Ascending, SearchSortDirection.Descending },
            Enum.GetValues<SearchSortDirection>());
        Assert.Equal(new[] { "Ascending", "Descending" }, Enum.GetNames<SearchSortDirection>());
    }

    /// <summary>
    /// 默认值为升序
    /// </summary>
    [Fact]
    public void Default_IsAscending()
    {
        Assert.Equal(SearchSortDirection.Ascending, default(SearchSortDirection));
    }
}
