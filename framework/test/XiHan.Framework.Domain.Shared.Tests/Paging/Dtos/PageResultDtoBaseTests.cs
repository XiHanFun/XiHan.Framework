// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Shared.Paging.Dtos;

namespace XiHan.Framework.Domain.Shared.Tests.Paging.Dtos;

/// <summary>
/// 分页响应基类的测试
/// </summary>
public class PageResultDtoBaseTests
{
    /// <summary>
    /// 空结果必须不含数据项且保留指定的分页参数
    /// </summary>
    [Fact]
    public void Empty_ReturnsNoItems_WithSpecifiedPaging()
    {
        var result = PageResultDtoBase<string>.Empty(2, 5);

        Assert.Empty(result.Items);
        Assert.Equal(2, result.Page.PageIndex);
        Assert.Equal(5, result.Page.PageSize);
        Assert.Equal(0, result.Page.TotalCount);
    }

    /// <summary>
    /// 从数据项与计数创建结果时必须构造正确的元数据
    /// </summary>
    [Fact]
    public void Create_FromItemsAndCounts_BuildsMetadata()
    {
        var items = new List<int> { 1, 2, 3 };

        var result = PageResultDtoBase<int>.Create(items, 1, 10, 3);

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(1, result.Page.PageIndex);
        Assert.Equal(10, result.Page.PageSize);
        Assert.Equal(3, result.Page.TotalCount);
    }

    /// <summary>
    /// 映射必须转换数据项并保留分页元数据
    /// </summary>
    [Fact]
    public void Map_TransformsItems_AndPreservesPageMetadata()
    {
        var source = PageResultDtoBase<int>.Create([1, 2, 3], 2, 10, 25);

        var mapped = source.Map(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(["1", "2", "3"], mapped.Items);
        Assert.Equal(source.Page.PageIndex, mapped.Page.PageIndex);
        Assert.Equal(source.Page.PageSize, mapped.Page.PageSize);
        Assert.Equal(source.Page.TotalCount, mapped.Page.TotalCount);
    }
}
