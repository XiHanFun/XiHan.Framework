// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Domain.Shared.Paging.Enums;
using XiHan.Framework.Domain.Shared.Paging.Executors;
using XiHan.Framework.Domain.Shared.Tests.Samples;

namespace XiHan.Framework.Domain.Shared.Tests.Paging.Executors;

/// <summary>
/// 分页查询执行器的测试
/// </summary>
/// <remarks>
/// 回归保护：Execute/ExecuteAsync 曾漏掉 Skip/Take，返回全部匹配记录而非当前页
/// （第 2 页也返回整表数据），此处按契约断言 Items 数量与内容均属于当前页。
/// </remarks>
public class PageQueryExecutorTests
{
    /// <summary>
    /// 构建样例数据：1..25 条，Name 为 user-01..user-25，Title 为 user
    /// </summary>
    private static List<QuerySampleEntity> BuildSamples()
    {
        return Enumerable.Range(1, 25)
            .Select(i => new QuerySampleEntity { Name = $"user-{i:D2}", Title = "user" })
            .ToList();
    }

    /// <summary>
    /// 同步分页：第 2 页每页 10 条必须返回第 11..20 条且总数正确
    /// </summary>
    [Fact]
    public void Execute_AppliesSkipAndTake_ForSecondPage()
    {
        var executor = new PageQueryExecutor<QuerySampleEntity>();
        var request = new PageRequestDtoBase().WithPage(2, 10).WithSort("userName");

        var result = executor.Execute(BuildSamples().AsQueryable(), request);

        Assert.Equal(25, result.Page.TotalCount);
        Assert.Equal(3, result.Page.TotalPages);
        Assert.Equal(10, result.Items.Count);
        Assert.Equal("user-11", result.Items[0].Name);
        Assert.Equal("user-20", result.Items[^1].Name);
    }

    /// <summary>
    /// 同步分页：末页只返回剩余记录
    /// </summary>
    [Fact]
    public void Execute_LastPage_ReturnsOnlyRemainingItems()
    {
        var executor = new PageQueryExecutor<QuerySampleEntity>();
        var request = new PageRequestDtoBase().WithPage(3, 10).WithSort("userName");

        var result = executor.Execute(BuildSamples().AsQueryable(), request);

        Assert.Equal(5, result.Items.Count);
        Assert.Equal("user-21", result.Items[0].Name);
        Assert.Equal("user-25", result.Items[^1].Name);
    }

    /// <summary>
    /// 同步分页：超页请求返回空 Items 但总数不变
    /// </summary>
    [Fact]
    public void Execute_BeyondLastPage_ReturnsEmptyItems_WithTotalCount()
    {
        var executor = new PageQueryExecutor<QuerySampleEntity>();
        var request = new PageRequestDtoBase().WithPage(9, 10).WithSort("userName");

        var result = executor.Execute(BuildSamples().AsQueryable(), request);

        Assert.Empty(result.Items);
        Assert.Equal(25, result.Page.TotalCount);
        Assert.Equal(0, result.Page.CurrentPageCount);
    }

    /// <summary>
    /// 异步分页：与同步路径一致应用 Skip/Take
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_AppliesSkipAndTake()
    {
        var executor = new PageQueryExecutor<QuerySampleEntity>();
        var request = new PageRequestDtoBase().WithPage(2, 10).WithSort("userName");

        var result = await executor.ExecuteAsync(
            BuildSamples().AsQueryable(), request, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(10, result.Items.Count);
        Assert.Equal("user-11", result.Items[0].Name);
        Assert.Equal(25, result.Page.TotalCount);
    }

    /// <summary>
    /// 过滤与分页组合：先过滤后分页，总数是过滤后的数量
    /// </summary>
    [Fact]
    public void Execute_FilterThenPage_UsesFilteredTotal()
    {
        var executor = new PageQueryExecutor<QuerySampleEntity>();
        // Name 别名 userName；Contains "user-0" 过滤出 user-01..user-09 共 9 条
        var request = new PageRequestDtoBase()
            .WithFilter("userName", "user-0", QueryOperator.Contains)
            .WithPage(2, 3)
            .WithSort("userName");

        var result = executor.Execute(BuildSamples().AsQueryable(), request);

        Assert.Equal(9, result.Page.TotalCount);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal("user-04", result.Items[0].Name);
        Assert.Equal("user-06", result.Items[^1].Name);
    }
}
