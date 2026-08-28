// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Strategy;

namespace XiHan.Framework.Bot.Tests;

/// <summary>
/// <see cref="FailoverStrategy"/> 测试
/// </summary>
/// <remarks>
/// 主备策略的核心契约：按传入顺序逐个尝试，首次成功即止；失败（含抛异常）要继续切到下一个，
/// 且每一次尝试都要留下带提供者名的明细，便于事后定位是谁挂了。
/// </remarks>
public class FailoverStrategyTests
{
    /// <summary>
    /// 策略名称是 Failover
    /// </summary>
    [Fact]
    public void Name_IsFailover()
    {
        Assert.Equal(BotStrategyNames.Failover, CreateStrategy().Name);
    }

    /// <summary>
    /// 首个提供者成功时不再尝试后续提供者
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenFirstSucceeds_StopsImmediately()
    {
        var primary = FakeBotProvider.AlwaysSuccess("A");
        var backup = FakeBotProvider.AlwaysSuccess("B");
        var context = CreateContext();

        await CreateStrategy().ExecuteAsync(context, [primary, backup]);

        Assert.Equal(1, primary.CallCount);
        Assert.Equal(0, backup.CallCount);
        Assert.Single(context.Results);
        Assert.True(context.IsSuccess);
    }

    /// <summary>
    /// 首个提供者失败后切到下一个，并把两次尝试都聚合进结果
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenFirstFails_SwitchesToNextAndAggregates()
    {
        var primary = FakeBotProvider.AlwaysFailed("A", "primary down");
        var backup = FakeBotProvider.AlwaysSuccess("B");
        var context = CreateContext();

        await CreateStrategy().ExecuteAsync(context, [primary, backup]);

        Assert.Equal(1, primary.CallCount);
        Assert.Equal(1, backup.CallCount);
        Assert.Equal(2, context.Results.Count);
        Assert.Equal("A", context.Results[0].Provider);
        Assert.False(context.Results[0].IsSuccess);
        Assert.Equal("primary down", context.Results[0].Message);
        Assert.Equal("B", context.Results[1].Provider);
        Assert.True(context.Results[1].IsSuccess);
    }

    /// <summary>
    /// 备用成功后不再继续尝试第三个提供者
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenBackupSucceeds_DoesNotTryRemaining()
    {
        var primary = FakeBotProvider.AlwaysFailed("A");
        var backup = FakeBotProvider.AlwaysSuccess("B");
        var last = FakeBotProvider.AlwaysSuccess("C");
        var context = CreateContext();

        await CreateStrategy().ExecuteAsync(context, [primary, backup, last]);

        Assert.Equal(1, backup.CallCount);
        Assert.Equal(0, last.CallCount);
        Assert.Equal(2, context.Results.Count);
    }

    /// <summary>
    /// 全部失败时每个提供者都被尝试过一次
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenAllFail_TriesEveryProvider()
    {
        var first = FakeBotProvider.AlwaysFailed("A");
        var second = FakeBotProvider.AlwaysFailed("B");
        var third = FakeBotProvider.AlwaysFailed("C");
        var context = CreateContext();

        await CreateStrategy().ExecuteAsync(context, [first, second, third]);

        Assert.Equal(1, first.CallCount);
        Assert.Equal(1, second.CallCount);
        Assert.Equal(1, third.CallCount);
        Assert.Equal(3, context.Results.Count);
        Assert.True(context.HasFailures);
        Assert.False(context.IsSuccess);
    }

    /// <summary>
    /// 首个提供者抛异常时按失败处理并切到下一个
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenFirstThrows_SwitchesToNext()
    {
        var throwing = FakeBotProvider.AlwaysThrows("A", "connection reset");
        var backup = FakeBotProvider.AlwaysSuccess("B");
        var context = CreateContext();

        await CreateStrategy().ExecuteAsync(context, [throwing, backup]);

        Assert.Equal(1, backup.CallCount);
        Assert.Equal(2, context.Results.Count);
        Assert.False(context.Results[0].IsSuccess);
        Assert.Equal("A", context.Results[0].Provider);
        Assert.Equal("connection reset", context.Results[0].Message);
    }

    /// <summary>
    /// 提供者列表为空时不产生结果也不抛出
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenNoProvider_DoesNothing()
    {
        var context = CreateContext();

        await CreateStrategy().ExecuteAsync(context, []);

        Assert.Empty(context.Results);
    }

    /// <summary>
    /// 令牌已取消时抛出取消异常且不触碰提供者
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenCancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var provider = FakeBotProvider.AlwaysSuccess("A");
        var context = new BotContext(new BotMessage { Content = "hi" }, [], cts.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => CreateStrategy().ExecuteAsync(context, [provider]));
        Assert.Equal(0, provider.CallCount);
    }

    private static FailoverStrategy CreateStrategy()
    {
        return new FailoverStrategy(NullLogger<FailoverStrategy>.Instance);
    }

    private static BotContext CreateContext()
    {
        return new BotContext(new BotMessage { Content = "hi" }, [], CancellationToken.None);
    }
}
