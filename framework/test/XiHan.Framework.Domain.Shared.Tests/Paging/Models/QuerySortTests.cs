// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Shared.Paging.Enums;
using XiHan.Framework.Domain.Shared.Paging.Models;

namespace XiHan.Framework.Domain.Shared.Tests.Paging.Models;

/// <summary>
/// 查询排序条件模型的测试
/// </summary>
public class QuerySortTests
{
    /// <summary>
    /// 构造函数必须去除字段名首尾空白并保留方向与优先级
    /// </summary>
    [Fact]
    public void Constructor_TrimsField_AndSetsDirectionAndPriority()
    {
        var sort = new QuerySort("  Name  ", SortDirection.Descending, 5);

        Assert.Equal("Name", sort.Field);
        Assert.Equal(SortDirection.Descending, sort.Direction);
        Assert.Equal(5, sort.Priority);
        Assert.True(sort.IsValid());
    }

    /// <summary>
    /// 字段名为空或纯空白时必须抛出参数异常
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Field_Blank_ThrowsArgumentException(string field)
    {
        Assert.Throws<ArgumentException>(() => new QuerySort(field));
    }

    /// <summary>
    /// 工厂方法必须生成指定方向的排序条件
    /// </summary>
    [Fact]
    public void Factories_ProduceExpectedDirection()
    {
        Assert.Equal(SortDirection.Ascending, QuerySort.Ascending("Name").Direction);
        Assert.Equal(SortDirection.Descending, QuerySort.Descending("Name").Direction);
        Assert.False(new QuerySort().IsValid());
    }
}
