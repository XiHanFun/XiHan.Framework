// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Strategy;
using XiHan.Framework.Bot.Tests.Fakes;

namespace XiHan.Framework.Bot.Tests.Strategy;

/// <summary>
/// <see cref="PriorityStrategy"/> 测试
/// </summary>
/// <remarks>
/// 该策略不做任何排序，"优先级"完全等价于传入列表的第一个元素，也就是 DI 注册顺序或渠道解析后的顺序；
/// 因此这里锁死的是"只发第一个、失败也不回退"这一行为，排序职责在上游。
/// </remarks>
public class PriorityStrategyTests
{
    /// <summary>
    /// 策略名称是 Priority
    /// </summary>
    [Fact]
    public void Name_IsPriority()
    {
        Assert.Equal(BotStrategyNames.Priority, CreateStrategy().Name);
    }

    /// <summary>
    /// 只把消息发给列表中的第一个提供者
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_SendsOnlyToFirstProvider()
    {
        var first = FakeBotProvider.AlwaysSuccess("A");
        var second = FakeBotProvider.AlwaysSuccess("B");
        var third = FakeBotProvider.AlwaysSuccess("C");
        var context = CreateContext();

        await CreateStrategy().ExecuteAsync(context, [first, second, third]);

        Assert.Equal(1, first.CallCount);
        Assert.Equal(0, second.CallCount);
        Assert.Equal(0, third.CallCount);
        Assert.Single(context.Results);
        Assert.Equal("A", context.Results[0].Provider);
    }

    /// <summary>
    /// 顺序由入参决定：换个顺序就换个目标提供者
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_FirstProviderIsDeterminedByArgumentOrder()
    {
        var first = FakeBotProvider.AlwaysSuccess("A");
        var second = FakeBotProvider.AlwaysSuccess("B");
        var context = CreateContext();

        await CreateStrategy().ExecuteAsync(context, [second, first]);

        Assert.Equal(1, second.CallCount);
        Assert.Equal(0, first.CallCount);
        Assert.Equal("B", context.Results[0].Provider);
    }

    /// <summary>
    /// 首个提供者失败时不回退到后续提供者
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenFirstFails_DoesNotFallBack()
    {
        var failing = FakeBotProvider.AlwaysFailed("A", "down");
        var healthy = FakeBotProvider.AlwaysSuccess("B");
        var context = CreateContext();

        await CreateStrategy().ExecuteAsync(context, [failing, healthy]);

        Assert.Equal(0, healthy.CallCount);
        Assert.Single(context.Results);
        Assert.False(context.Results[0].IsSuccess);
    }

    /// <summary>
    /// 首个提供者抛异常时兜成失败结果
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenFirstThrows_RecordsFailure()
    {
        var throwing = FakeBotProvider.AlwaysThrows("A", "dns failure");
        var context = CreateContext();

        await CreateStrategy().ExecuteAsync(context, [throwing]);

        Assert.Single(context.Results);
        Assert.False(context.Results[0].IsSuccess);
        Assert.Equal("A", context.Results[0].Provider);
        Assert.Equal("dns failure", context.Results[0].Message);
    }

    /// <summary>
    /// 提供者列表为空时直接返回，不产生结果也不抛出
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenNoProvider_ReturnsWithoutResult()
    {
        var context = CreateContext();

        await CreateStrategy().ExecuteAsync(context, []);

        Assert.Empty(context.Results);
    }

    /// <summary>
    /// 提供者列表为空时即便令牌已取消也不抛出（取消检查在取到提供者之后）
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenNoProviderAndCancelled_DoesNotThrow()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var context = new BotContext(new BotMessage { Content = "hi" }, [], cts.Token);

        await CreateStrategy().ExecuteAsync(context, []);

        Assert.Empty(context.Results);
    }

    /// <summary>
    /// 有提供者且令牌已取消时抛出取消异常
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

    private static PriorityStrategy CreateStrategy()
    {
        return new PriorityStrategy(NullLogger<PriorityStrategy>.Instance);
    }

    private static BotContext CreateContext()
    {
        return new BotContext(new BotMessage { Content = "hi" }, [], CancellationToken.None);
    }
}
