// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Options;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Strategy;

namespace XiHan.Framework.Bot.Tests;

/// <summary>
/// <see cref="BroadcastStrategy"/> 测试
/// </summary>
/// <remarks>
/// 广播策略要把消息发给每一个提供者；ContinueOnError 决定遇到失败是继续还是当场停下。
/// 提供者抛出的异常必须被兜成失败结果，不能穿透到调度器之外。
/// </remarks>
public class BroadcastStrategyTests
{
    /// <summary>
    /// 策略名称是 Broadcast
    /// </summary>
    [Fact]
    public void Name_IsBroadcast()
    {
        Assert.Equal(BotStrategyNames.Broadcast, CreateStrategy(new XiHanBotOptions()).Name);
    }

    /// <summary>
    /// 所有提供者都收到消息并按顺序记录结果
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_SendsToEveryProvider()
    {
        var first = FakeBotProvider.AlwaysSuccess("A");
        var second = FakeBotProvider.AlwaysSuccess("B");
        var context = CreateContext();

        await CreateStrategy(new XiHanBotOptions()).ExecuteAsync(context, [first, second]);

        Assert.Equal(1, first.CallCount);
        Assert.Equal(1, second.CallCount);
        Assert.Equal(2, context.Results.Count);
        Assert.Equal("A", context.Results[0].Provider);
        Assert.Equal("B", context.Results[1].Provider);
        Assert.True(context.IsSuccess);
    }

    /// <summary>
    /// 默认允许失败继续，后续提供者仍会收到消息
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenContinueOnError_KeepsSendingAfterFailure()
    {
        var failing = FakeBotProvider.AlwaysFailed("A", "down");
        var healthy = FakeBotProvider.AlwaysSuccess("B");
        var context = CreateContext();

        await CreateStrategy(new XiHanBotOptions()).ExecuteAsync(context, [failing, healthy]);

        Assert.Equal(1, failing.CallCount);
        Assert.Equal(1, healthy.CallCount);
        Assert.Equal(2, context.Results.Count);
        Assert.True(context.HasFailures);
        Assert.False(context.IsSuccess);
    }

    /// <summary>
    /// 关闭 ContinueOnError 后首个失败即停止
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenContinueOnErrorDisabled_StopsAtFirstFailure()
    {
        var failing = FakeBotProvider.AlwaysFailed("A", "down");
        var healthy = FakeBotProvider.AlwaysSuccess("B");
        var context = CreateContext();
        var options = new XiHanBotOptions { ContinueOnError = false };

        await CreateStrategy(options).ExecuteAsync(context, [failing, healthy]);

        Assert.Equal(1, failing.CallCount);
        Assert.Equal(0, healthy.CallCount);
        Assert.Single(context.Results);
    }

    /// <summary>
    /// 关闭 ContinueOnError 但一路成功时不会提前中断
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenContinueOnErrorDisabledAndAllSucceed_SendsToAll()
    {
        var first = FakeBotProvider.AlwaysSuccess("A");
        var second = FakeBotProvider.AlwaysSuccess("B");
        var context = CreateContext();
        var options = new XiHanBotOptions { ContinueOnError = false };

        await CreateStrategy(options).ExecuteAsync(context, [first, second]);

        Assert.Equal(1, first.CallCount);
        Assert.Equal(1, second.CallCount);
        Assert.Equal(2, context.Results.Count);
    }

    /// <summary>
    /// 提供者抛异常时兜成失败结果并带上提供者名与异常消息
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenProviderThrows_RecordsFailureAndContinues()
    {
        var throwing = FakeBotProvider.AlwaysThrows("A", "socket closed");
        var healthy = FakeBotProvider.AlwaysSuccess("B");
        var context = CreateContext();

        await CreateStrategy(new XiHanBotOptions()).ExecuteAsync(context, [throwing, healthy]);

        Assert.Equal(2, context.Results.Count);
        Assert.False(context.Results[0].IsSuccess);
        Assert.Equal("A", context.Results[0].Provider);
        Assert.Equal("socket closed", context.Results[0].Message);
        Assert.True(context.Results[1].IsSuccess);
    }

    /// <summary>
    /// 提供者列表为空时不产生结果也不抛出
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenNoProvider_DoesNothing()
    {
        var context = CreateContext();

        await CreateStrategy(new XiHanBotOptions()).ExecuteAsync(context, []);

        Assert.Empty(context.Results);
        Assert.False(context.IsSuccess);
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
            () => CreateStrategy(new XiHanBotOptions()).ExecuteAsync(context, [provider]));
        Assert.Equal(0, provider.CallCount);
    }

    private static BroadcastStrategy CreateStrategy(XiHanBotOptions options)
    {
        return new BroadcastStrategy(
            new TestOptionsWrapper<XiHanBotOptions>(options),
            NullLogger<BroadcastStrategy>.Instance);
    }

    private static BotContext CreateContext()
    {
        return new BotContext(new BotMessage { Content = "hi" }, [], CancellationToken.None);
    }
}
