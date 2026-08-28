// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.SearchEngines.Abstractions.Querying;

namespace XiHan.Framework.SearchEngines.Abstractions.Tests.Querying;

/// <summary>
/// 结构化过滤运算符的测试
/// </summary>
/// <remarks>
/// 运算符会随查询条件一起被序列化传输或落库（前端查询条件、保存的检索方案），
/// 因此成员名与序号一并锁死，新增只允许追加到末尾。
/// </remarks>
public class SearchFilterOperatorTests
{
    /// <summary>
    /// 各成员的序号不漂移
    /// </summary>
    /// <param name="op">运算符</param>
    /// <param name="expected">期望序号</param>
    [Theory]
    [InlineData(SearchFilterOperator.Equal, 0)]
    [InlineData(SearchFilterOperator.NotEqual, 1)]
    [InlineData(SearchFilterOperator.In, 2)]
    [InlineData(SearchFilterOperator.GreaterThan, 3)]
    [InlineData(SearchFilterOperator.GreaterThanOrEqual, 4)]
    [InlineData(SearchFilterOperator.LessThan, 5)]
    [InlineData(SearchFilterOperator.LessThanOrEqual, 6)]
    [InlineData(SearchFilterOperator.Exists, 7)]
    [InlineData(SearchFilterOperator.StartsWith, 8)]
    public void Value_IsStable(SearchFilterOperator op, int expected)
    {
        Assert.Equal(expected, (int)op);
    }

    /// <summary>
    /// 成员集合与顺序不漂移
    /// </summary>
    [Fact]
    public void Members_AreExactlyTheBackendIntersection()
    {
        Assert.Equal(
            [
                SearchFilterOperator.Equal,
                SearchFilterOperator.NotEqual,
                SearchFilterOperator.In,
                SearchFilterOperator.GreaterThan,
                SearchFilterOperator.GreaterThanOrEqual,
                SearchFilterOperator.LessThan,
                SearchFilterOperator.LessThanOrEqual,
                SearchFilterOperator.Exists,
                SearchFilterOperator.StartsWith
            ],
            Enum.GetValues<SearchFilterOperator>());
    }

    /// <summary>
    /// 成员名称不漂移
    /// </summary>
    [Fact]
    public void Names_AreStable()
    {
        Assert.Equal(
            new[]
            {
                "Equal", "NotEqual", "In", "GreaterThan", "GreaterThanOrEqual",
                "LessThan", "LessThanOrEqual", "Exists", "StartsWith"
            },
            Enum.GetNames<SearchFilterOperator>());
    }

    /// <summary>
    /// 越界数值不属于已定义成员
    /// </summary>
    [Fact]
    public void IsDefined_ForOutOfRangeValue_IsFalse()
    {
        Assert.False(Enum.IsDefined((SearchFilterOperator)9));
        Assert.False(Enum.IsDefined((SearchFilterOperator)(-1)));
    }

    /// <summary>
    /// 默认值为等于
    /// </summary>
    [Fact]
    public void Default_IsEqual()
    {
        Assert.Equal(SearchFilterOperator.Equal, default(SearchFilterOperator));
    }
}
