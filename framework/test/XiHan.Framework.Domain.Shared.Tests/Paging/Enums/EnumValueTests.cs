// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Shared.Paging.Enums;

namespace XiHan.Framework.Domain.Shared.Tests.Paging.Enums;

/// <summary>
/// 查询枚举取值的测试
/// </summary>
public class EnumValueTests
{
    /// <summary>
    /// 排序方向与关键字匹配模式的枚举值必须稳定
    /// </summary>
    [Fact]
    public void SortDirection_And_KeywordMatchMode_HaveStableValues()
    {
        Assert.Equal(1000, (int)SortDirection.Ascending);
        Assert.Equal(1001, (int)SortDirection.Descending);

        Assert.Equal(1000, (int)KeywordMatchMode.Contains);
        Assert.Equal(1001, (int)KeywordMatchMode.StartsWith);
        Assert.Equal(1002, (int)KeywordMatchMode.EndsWith);
        Assert.Equal(1003, (int)KeywordMatchMode.Exact);
    }

    /// <summary>
    /// 查询操作符的枚举值必须稳定
    /// </summary>
    [Fact]
    public void QueryOperator_HasStableValues()
    {
        Assert.Equal(1000, (int)QueryOperator.Equal);
        Assert.Equal(1001, (int)QueryOperator.NotEqual);
        Assert.Equal(2000, (int)QueryOperator.Contains);
        Assert.Equal(3000, (int)QueryOperator.In);
        Assert.Equal(4000, (int)QueryOperator.Between);
        Assert.Equal(5000, (int)QueryOperator.IsNull);
        Assert.Equal(5001, (int)QueryOperator.IsNotNull);
    }
}
