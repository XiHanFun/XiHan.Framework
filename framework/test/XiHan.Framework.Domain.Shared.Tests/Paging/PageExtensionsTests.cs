// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Shared.Paging;
using XiHan.Framework.Domain.Shared.Paging.Dtos;

namespace XiHan.Framework.Domain.Shared.Tests.Paging;

/// <summary>
/// 分页扩展方法的测试
/// </summary>
public class PageExtensionsTests
{
    /// <summary>
    /// 异步分页必须返回当前页数据与正确的总数
    /// </summary>
    [Fact]
    public async Task ToPageResultAsync_ReturnsPagedItems_AndTotalCount()
    {
        var query = Enumerable.Range(1, 25).AsQueryable();
        var request = new PageRequestDtoBase().WithPage(2, 10);

        var result = await query.ToPageResultAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(25, result.Page.TotalCount);
        Assert.Equal(3, result.Page.TotalPages);
        Assert.Equal(10, result.Items.Count);
        Assert.Equal(11, result.Items[0]);
        Assert.Equal(20, result.Items[^1]);
    }

    /// <summary>
    /// 跳页超过最后一页时必须返回空数据项且总数保持不变
    /// </summary>
    [Fact]
    public void ToPageResult_BeyondLastPage_ReturnsEmptyItems_WithCorrectTotal()
    {
        var query = Enumerable.Range(1, 25).AsQueryable();
        var request = new PageRequestDtoBase().WithPage(5, 10);

        var result = query.ToPageResult(request);

        Assert.Equal(25, result.Page.TotalCount);
        Assert.Empty(result.Items);
    }
}
