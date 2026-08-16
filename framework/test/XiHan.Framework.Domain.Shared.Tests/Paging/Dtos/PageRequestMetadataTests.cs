// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Shared.Paging.Models;

namespace XiHan.Framework.Domain.Shared.Tests.Paging.Dtos;

/// <summary>
/// 分页请求元数据的测试
/// </summary>
public class PageRequestMetadataTests
{
    /// <summary>
    /// 默认构造必须给出第 1 页、每页 20 条
    /// </summary>
    [Fact]
    public void Default_Constructor_UsesFirstPage_AndTwentyPerPage()
    {
        var metadata = new PageRequestMetadata();

        Assert.Equal(PageRequestMetadata.DefaultPageIndex, metadata.PageIndex);
        Assert.Equal(PageRequestMetadata.DefaultPageSize, metadata.PageSize);
    }

    /// <summary>
    /// 页码小于 1 时必须被夹取为 1
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void PageIndex_BelowMinimum_ClampsToDefault(int pageIndex)
    {
        var metadata = new PageRequestMetadata(pageIndex, 50);

        Assert.Equal(PageRequestMetadata.DefaultPageIndex, metadata.PageIndex);
        Assert.Equal(50, metadata.PageSize);
    }

    /// <summary>
    /// 每页大小越界时必须被夹取到合法区间
    /// </summary>
    [Theory]
    [InlineData(0, PageRequestMetadata.DefaultPageSize)]
    [InlineData(-1, PageRequestMetadata.DefaultPageSize)]
    [InlineData(501, PageRequestMetadata.MaxPageSize)]
    [InlineData(10000, PageRequestMetadata.MaxPageSize)]
    [InlineData(250, 250)]
    public void PageSize_OutOfRange_ClampsToBounds(int pageSize, int expected)
    {
        var metadata = new PageRequestMetadata(2, pageSize);

        Assert.Equal(expected, metadata.PageSize);
    }
}
