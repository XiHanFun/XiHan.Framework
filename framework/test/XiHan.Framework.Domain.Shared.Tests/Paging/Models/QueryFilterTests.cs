// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Shared.Paging.Enums;
using XiHan.Framework.Domain.Shared.Paging.Models;

namespace XiHan.Framework.Domain.Shared.Tests.Paging.Models;

/// <summary>
/// 查询过滤条件模型的测试
/// </summary>
public class QueryFilterTests
{
    /// <summary>
    /// 为空与不为空操作符不需要值即视为有效
    /// </summary>
    [Theory]
    [InlineData(QueryOperator.IsNull)]
    [InlineData(QueryOperator.IsNotNull)]
    public void IsNull_And_IsNotNull_DoNotRequireValue(QueryOperator @operator)
    {
        var filter = new QueryFilter("Name", null, @operator);

        Assert.True(filter.IsValid());
    }

    /// <summary>
    /// 等于操作符在值为空时必须视为无效
    /// </summary>
    [Fact]
    public void Equal_WithNullValue_IsInvalid()
    {
        var filter = new QueryFilter("Name", null, QueryOperator.Equal);

        Assert.False(filter.IsValid());
    }

    /// <summary>
    /// In 操作符必须至少携带一个值
    /// </summary>
    [Fact]
    public void In_RequiresAtLeastOneValue()
    {
        Assert.False(new QueryFilter("Id", [], QueryOperator.In).IsValid());
        Assert.True(new QueryFilter("Id", [1], QueryOperator.In).IsValid());
    }

    /// <summary>
    /// Between 操作符必须恰好携带两个值
    /// </summary>
    [Fact]
    public void Between_RequiresExactlyTwoValues()
    {
        Assert.False(new QueryFilter("Age", [1], QueryOperator.Between).IsValid());
        Assert.True(new QueryFilter("Age", [1, 10], QueryOperator.Between).IsValid());
    }

    /// <summary>
    /// 工厂方法必须生成正确的操作符与值
    /// </summary>
    [Fact]
    public void Factories_ProduceExpectedOperatorAndValues()
    {
        Assert.Equal(QueryOperator.Contains, QueryFilter.Contains("Name", "x").Operator);
        Assert.Equal(QueryOperator.In, QueryFilter.In("Id", 1, 2, 3).Operator);
        Assert.Equal(3, QueryFilter.In("Id", 1, 2, 3).Values!.Length);
        Assert.Equal(QueryOperator.Between, QueryFilter.Between("Age", 1, 10).Operator);
        Assert.Equal(2, QueryFilter.Between("Age", 1, 10).Values!.Length);
        Assert.Equal(QueryOperator.IsNull, QueryFilter.IsNull("Name").Operator);
    }

    /// <summary>
    /// 可读字符串必须正确描述过滤条件
    /// </summary>
    [Fact]
    public void ToString_ProducesReadableRepresentation()
    {
        Assert.Equal("Name CONTAINS 'zhang'", QueryFilter.Contains("Name", "zhang").ToString());
        Assert.Equal("Age BETWEEN 1 AND 10", QueryFilter.Between("Age", 1, 10).ToString());
        Assert.Equal("Name IS NULL", QueryFilter.IsNull("Name").ToString());
    }
}
