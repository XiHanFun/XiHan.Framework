// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Shared.Paging.Models;

namespace XiHan.Framework.Domain.Shared.Tests.Paging.Dtos;

/// <summary>
/// 分页响应元数据的测试
/// </summary>
public class PageResultMetadataTests
{
    /// <summary>
    /// 默认构造必须给出第 1 页、每页 20 条、总记录数为 0
    /// </summary>
    [Fact]
    public void Default_Constructor_UsesFirstPage_TwentyPerPage_ZeroTotal()
    {
        var metadata = new PageResultMetadata();

        Assert.Equal(1, metadata.PageIndex);
        Assert.Equal(20, metadata.PageSize);
        Assert.Equal(0, metadata.TotalCount);
        Assert.Equal(0, metadata.TotalPages);
    }

    /// <summary>
    /// 总页数必须为总记录数除以每页大小后的向上取整
    /// </summary>
    [Theory]
    [InlineData(25, 10, 3)]
    [InlineData(20, 10, 2)]
    [InlineData(0, 10, 0)]
    [InlineData(1, 20, 1)]
    public void TotalPages_IsCeilingOfTotalDividedBySize(int totalCount, int pageSize, int expected)
    {
        var metadata = new PageResultMetadata(1, pageSize, totalCount);

        Assert.Equal(expected, metadata.TotalPages);
    }

    /// <summary>
    /// 导航属性必须正确反映当前页位置
    /// </summary>
    [Fact]
    public void Navigation_Properties_ReflectCurrentPage()
    {
        var first = new PageResultMetadata(1, 10, 25);
        Assert.True(first.IsFirstPage);
        Assert.False(first.HasPrevious);
        Assert.True(first.HasNext);
        Assert.False(first.IsLastPage);

        var last = new PageResultMetadata(3, 10, 25);
        Assert.False(last.HasNext);
        Assert.True(last.IsLastPage);
        Assert.True(last.HasPrevious);
    }

    /// <summary>
    /// 记录范围必须根据页码、每页大小与总数正确计算
    /// </summary>
    [Theory]
    [InlineData(2, 10, 25, 11, 20, 10)]
    [InlineData(3, 10, 25, 21, 25, 5)]
    [InlineData(1, 20, 0, 0, 0, 0)]
    public void RecordRange_IsComputedFromPageAndTotal(int pageIndex, int pageSize, int totalCount,
        int expectedStart, int expectedEnd, int expectedCount)
    {
        var metadata = new PageResultMetadata(pageIndex, pageSize, totalCount);

        Assert.Equal(expectedStart, metadata.StartRecord);
        Assert.Equal(expectedEnd, metadata.EndRecord);
        Assert.Equal(expectedCount, metadata.CurrentPageCount);
    }
}
