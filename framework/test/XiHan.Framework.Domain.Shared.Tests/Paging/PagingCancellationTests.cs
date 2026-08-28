// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Shared.Paging;
using XiHan.Framework.Domain.Shared.Paging.Converters;
using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Domain.Shared.Paging.Executors;
using XiHan.Framework.Domain.Shared.Tests.Samples;

namespace XiHan.Framework.Domain.Shared.Tests.Paging;

/// <summary>
/// 分页异步入口的取消令牌契约测试
/// </summary>
/// <remarks>
/// 回归保护：这三个方法此前都声明了 CancellationToken 形参却从不使用，
/// 调用方（SqlSugarReadOnlyRepository 的三个分页方法）一路把令牌传进来，
/// 取消请求却对分页查询完全不起作用。
/// 这里锁住最基本的一条契约：传入已取消的令牌必须抛出 OperationCanceledException，
/// 而不是照常跑完返回结果。
/// </remarks>
public class PagingCancellationTests
{
    /// <summary>
    /// 构造一份足够触发取页逻辑的样本数据
    /// </summary>
    private static IQueryable<QuerySampleEntity> BuildSource()
    {
        return Enumerable.Range(1, 20)
            .Select(i => new QuerySampleEntity { Name = "n" + i, Age = i })
            .AsQueryable();
    }

    /// <summary>
    /// 构造默认分页请求
    /// </summary>
    private static PageRequestDtoBase BuildRequest()
    {
        return new PageRequestDtoBase().WithPage(1, 5);
    }

    /// <summary>
    /// 执行器收到已取消的令牌时抛出，而不是照常返回分页结果
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenTokenAlreadyCancelled_Throws()
    {
        var executor = new PageQueryExecutor<QuerySampleEntity>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => executor.ExecuteAsync(BuildSource(), BuildRequest(), true, cts.Token));
    }

    /// <summary>
    /// 未取消时执行器正常返回，取消检查不影响正常路径
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenTokenNotCancelled_ReturnsPage()
    {
        var executor = new PageQueryExecutor<QuerySampleEntity>();

        var result = await executor.ExecuteAsync(
            BuildSource(), BuildRequest(), true, TestContext.Current.CancellationToken);

        Assert.Equal(20, result.Page.TotalCount);
        Assert.Equal(5, result.Items.Count);
    }

    /// <summary>
    /// 分页扩展收到已取消的令牌时抛出
    /// </summary>
    [Fact]
    public async Task ToPageResultAsync_WhenTokenAlreadyCancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => BuildSource().ToPageResultAsync(BuildRequest(), cts.Token));
    }

    /// <summary>
    /// 未取消时分页扩展正常返回
    /// </summary>
    [Fact]
    public async Task ToPageResultAsync_WhenTokenNotCancelled_ReturnsPage()
    {
        var result = await BuildSource().ToPageResultAsync(
            BuildRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(20, result.Page.TotalCount);
        Assert.Equal(5, result.Items.Count);
    }

    /// <summary>
    /// 结果转换收到已取消的令牌时抛出，且一个转换任务都不启动
    /// </summary>
    [Fact]
    public async Task ConvertItemsAsync_WhenTokenAlreadyCancelled_ThrowsWithoutInvokingConverter()
    {
        var source = new PageResultDtoBase<int>([1, 2, 3], 1, 10, 3);
        var invoked = 0;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => source.ConvertItemsAsync(
                item =>
                {
                    Interlocked.Increment(ref invoked);
                    return Task.FromResult(item.ToString());
                },
                cts.Token));

        Assert.Equal(0, invoked);
    }

    /// <summary>
    /// 未取消时结果转换正常完成
    /// </summary>
    [Fact]
    public async Task ConvertItemsAsync_WhenTokenNotCancelled_ConvertsEveryItem()
    {
        var source = new PageResultDtoBase<int>([1, 2, 3], 1, 10, 3);

        var result = await source.ConvertItemsAsync(
            item => Task.FromResult(item.ToString()),
            TestContext.Current.CancellationToken);

        Assert.Equal(["1", "2", "3"], result.Items);
    }
}
