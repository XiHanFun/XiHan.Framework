// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Options;
using XiHan.Framework.Bot.Pipeline;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Strategy;

namespace XiHan.Framework.Bot.Tests;

/// <summary>
/// <see cref="BotDispatcher"/> 测试
/// </summary>
/// <remarks>
/// 调度器负责四件事：解析提供者、选策略、按注册顺序套管道、把上下文结果聚合成 <c>BotDispatchResult</c>。
/// 所有提供者都是手写替身，不发起任何网络请求。
/// </remarks>
public class BotDispatcherTests
{
    /// <summary>
    /// 消息为 null 时抛出参数空异常
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenMessageNull_Throws()
    {
        var dispatcher = CreateDispatcher(new XiHanBotOptions(), FakeBotProvider.AlwaysSuccess("A"));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => dispatcher.DispatchAsync(null!, null, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 令牌已取消时立即抛出取消异常，不触碰任何提供者
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenTokenCancelled_ThrowsBeforeSending()
    {
        var provider = FakeBotProvider.AlwaysSuccess("A");
        var dispatcher = CreateDispatcher(new XiHanBotOptions(), provider);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => dispatcher.DispatchAsync(new BotMessage { Content = "hi" }, null, cts.Token));
        Assert.Equal(0, provider.CallCount);
    }

    /// <summary>
    /// 没有可用提供者时返回失败聚合结果而不抛出
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenNoProvider_ReturnsNoProviderResult()
    {
        var dispatcher = CreateDispatcher(new XiHanBotOptions());

        var result = await dispatcher.DispatchAsync(new BotMessage { Content = "hi" }, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsSkipped);
        Assert.Equal("No bot provider configured.", result.ErrorMessage);
        Assert.Empty(result.Results);
    }

    /// <summary>
    /// 开启 ThrowWhenNoProvider 后无提供者直接抛出
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenNoProviderAndThrowEnabled_Throws()
    {
        var options = new XiHanBotOptions { ThrowWhenNoProvider = true };
        var dispatcher = CreateDispatcher(options);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.DispatchAsync(new BotMessage { Content = "hi" }, null, TestContext.Current.CancellationToken));

        Assert.Equal("No bot provider configured.", exception.Message);
    }

    /// <summary>
    /// 指定的渠道一个都匹配不上时按无提供者处理
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenChannelsMatchNothing_ReturnsNoProviderResult()
    {
        var provider = FakeBotProvider.AlwaysSuccess("A");
        var dispatcher = CreateDispatcher(new XiHanBotOptions(), provider);

        var result = await dispatcher.DispatchAsync(
            new BotMessage { Content = "hi" },
            ["NotExists"],
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, provider.CallCount);
    }

    /// <summary>
    /// 指定渠道时只发往命中的提供者
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WithChannels_OnlyMatchedProvidersReceive()
    {
        var first = FakeBotProvider.AlwaysSuccess("A");
        var second = FakeBotProvider.AlwaysSuccess("B");
        var dispatcher = CreateDispatcher(new XiHanBotOptions(), first, second);

        var result = await dispatcher.DispatchAsync(
            new BotMessage { Content = "hi" },
            ["B"],
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Results);
        Assert.Equal(0, first.CallCount);
        Assert.Equal(1, second.CallCount);
    }

    /// <summary>
    /// 渠道列表里的空白项被剔除后仍视为"未指定渠道"，走全量广播
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenChannelsAllBlank_BroadcastsToAll()
    {
        var first = FakeBotProvider.AlwaysSuccess("A");
        var second = FakeBotProvider.AlwaysSuccess("B");
        var dispatcher = CreateDispatcher(new XiHanBotOptions(), first, second);

        var result = await dispatcher.DispatchAsync(
            new BotMessage { Content = "hi" },
            ["  ", string.Empty],
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Results.Count);
        Assert.Equal(1, first.CallCount);
        Assert.Equal(1, second.CallCount);
    }

    /// <summary>
    /// 默认走广播策略，所有提供者都收到消息
    /// </summary>
    [Fact]
    public async Task DispatchAsync_ByDefault_UsesBroadcastStrategy()
    {
        var first = FakeBotProvider.AlwaysSuccess("A");
        var second = FakeBotProvider.AlwaysSuccess("B");
        var dispatcher = CreateDispatcher(new XiHanBotOptions(), first, second);

        var result = await dispatcher.DispatchAsync(new BotMessage { Content = "hi" }, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Results.Count);
        Assert.Equal(1, first.CallCount);
        Assert.Equal(1, second.CallCount);
    }

    /// <summary>
    /// 选项里的默认策略生效
    /// </summary>
    [Fact]
    public async Task DispatchAsync_UsesDefaultStrategyFromOptions()
    {
        var options = new XiHanBotOptions { DefaultStrategy = BotStrategyNames.Priority };
        var first = FakeBotProvider.AlwaysSuccess("A");
        var second = FakeBotProvider.AlwaysSuccess("B");
        var dispatcher = CreateDispatcher(options, first, second);

        var result = await dispatcher.DispatchAsync(new BotMessage { Content = "hi" }, null, TestContext.Current.CancellationToken);

        Assert.Single(result.Results);
        Assert.Equal(1, first.CallCount);
        Assert.Equal(0, second.CallCount);
    }

    /// <summary>
    /// 策略名大小写不敏感
    /// </summary>
    [Fact]
    public async Task DispatchAsync_StrategyNameMatchIsCaseInsensitive()
    {
        var first = FakeBotProvider.AlwaysSuccess("A");
        var second = FakeBotProvider.AlwaysSuccess("B");
        var dispatcher = CreateDispatcher(new XiHanBotOptions(), first, second);
        var message = new BotMessage { Content = "hi" };
        message.Data[BotMessageDataKeys.Strategy] = "priority";

        await dispatcher.DispatchAsync(message, null, TestContext.Current.CancellationToken);

        Assert.Equal(1, first.CallCount);
        Assert.Equal(0, second.CallCount);
    }

    /// <summary>
    /// 未知策略名回退到广播策略
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenStrategyUnknown_FallsBackToBroadcast()
    {
        var first = FakeBotProvider.AlwaysSuccess("A");
        var second = FakeBotProvider.AlwaysSuccess("B");
        var dispatcher = CreateDispatcher(new XiHanBotOptions(), first, second);
        var message = new BotMessage { Content = "hi" };
        message.Data[BotMessageDataKeys.Strategy] = "NotExists";

        var result = await dispatcher.DispatchAsync(message, null, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Results.Count);
    }

    /// <summary>
    /// 一个策略都没注册时抛出并带上策略名
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenNoStrategyRegistered_Throws()
    {
        var options = new XiHanBotOptions();
        var wrapped = new TestOptionsWrapper<XiHanBotOptions>(options);
        var manager = new BotProviderManager([FakeBotProvider.AlwaysSuccess("A")], wrapped);
        var dispatcher = new BotDispatcher(
            manager,
            [],
            [],
            wrapped,
            NullLogger<BotDispatcher>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.DispatchAsync(new BotMessage { Content = "hi" }, null, TestContext.Current.CancellationToken));

        Assert.Contains("Broadcast", exception.Message);
        Assert.Contains("is not registered", exception.Message);
    }

    /// <summary>
    /// 主备策略在首个提供者失败后切到下一个，并把两条明细都聚合进结果
    /// </summary>
    /// <remarks>
    /// 这里只断言"切换发生了、明细齐全、备用提供者成功"，不断言整体 IsSuccess——
    /// 后者受 <c>BotDispatchResult.From</c> 的全成功口径影响，已列入疑似缺陷交由主控裁决。
    /// </remarks>
    [Fact]
    public async Task DispatchAsync_WithFailoverStrategy_SwitchesToNextProvider()
    {
        var primary = FakeBotProvider.AlwaysFailed("A", "primary down");
        var backup = FakeBotProvider.AlwaysSuccess("B");
        var dispatcher = CreateDispatcher(new XiHanBotOptions(), primary, backup);
        var message = new BotMessage { Content = "hi" };
        message.Data[BotMessageDataKeys.Strategy] = BotStrategyNames.Failover;

        var result = await dispatcher.DispatchAsync(message, null, TestContext.Current.CancellationToken);

        Assert.Equal(1, primary.CallCount);
        Assert.Equal(1, backup.CallCount);
        Assert.Equal(2, result.Results.Count);
        Assert.Equal("A", result.Results[0].Provider);
        Assert.False(result.Results[0].IsSuccess);
        Assert.Equal("B", result.Results[1].Provider);
        Assert.True(result.Results[1].IsSuccess);
    }

    /// <summary>
    /// 管道按注册顺序由外向内包裹，最内层才是策略
    /// </summary>
    [Fact]
    public async Task DispatchAsync_InvokesPipelinesInRegistrationOrder()
    {
        var trace = new List<string>();
        var options = new XiHanBotOptions();
        var wrapped = new TestOptionsWrapper<XiHanBotOptions>(options);
        var provider = FakeBotProvider.AlwaysSuccess("A");
        var manager = new BotProviderManager([provider], wrapped);
        var dispatcher = new BotDispatcher(
            manager,
            [
                new RecordingPipeline("outer", trace),
                new RecordingPipeline("inner", trace)
            ],
            [new BroadcastStrategy(wrapped, NullLogger<BroadcastStrategy>.Instance)],
            wrapped,
            NullLogger<BotDispatcher>.Instance);

        await dispatcher.DispatchAsync(new BotMessage { Content = "hi" }, null, TestContext.Current.CancellationToken);

        Assert.Equal(4, trace.Count);
        Assert.Equal("outer:enter", trace[0]);
        Assert.Equal("inner:enter", trace[1]);
        Assert.Equal("inner:exit", trace[2]);
        Assert.Equal("outer:exit", trace[3]);
    }

    /// <summary>
    /// 管道短路置位跳过标记时，聚合结果标记为已跳过且不成功
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenPipelineSkips_ReturnsSkippedResult()
    {
        var trace = new List<string>();
        var options = new XiHanBotOptions();
        var wrapped = new TestOptionsWrapper<XiHanBotOptions>(options);
        var provider = FakeBotProvider.AlwaysSuccess("A");
        var manager = new BotProviderManager([provider], wrapped);
        var dispatcher = new BotDispatcher(
            manager,
            [new RecordingPipeline("filter", trace, shortCircuit: true)],
            [new BroadcastStrategy(wrapped, NullLogger<BroadcastStrategy>.Instance)],
            wrapped,
            NullLogger<BotDispatcher>.Instance);

        var result = await dispatcher.DispatchAsync(new BotMessage { Content = "hi" }, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSkipped);
        Assert.False(result.IsSuccess);
        Assert.Equal("Bot dispatch skipped.", result.ErrorMessage);
        Assert.Empty(result.Results);
        Assert.Equal(0, provider.CallCount);
    }

    /// <summary>
    /// 提供者抛异常时被策略兜住，聚合为失败明细而不是把异常抛给调用方
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenProviderThrows_AggregatesAsFailure()
    {
        var provider = FakeBotProvider.AlwaysThrows("A", "socket closed");
        var dispatcher = CreateDispatcher(new XiHanBotOptions(), provider);

        var result = await dispatcher.DispatchAsync(new BotMessage { Content = "hi" }, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Single(result.Results);
        Assert.Equal("A", result.Results[0].Provider);
        Assert.Equal("socket closed", result.Results[0].Message);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("A:socket closed", result.ErrorMessage!);
    }

    /// <summary>
    /// 消息原样传给提供者，调度器不做内容改写
    /// </summary>
    [Fact]
    public async Task DispatchAsync_PassesMessageThroughUnchanged()
    {
        var provider = FakeBotProvider.AlwaysSuccess("A");
        var dispatcher = CreateDispatcher(new XiHanBotOptions(), provider);
        var message = new BotMessage { Content = "hi", Title = "t" };

        await dispatcher.DispatchAsync(message, null, TestContext.Current.CancellationToken);

        Assert.Same(message, provider.LastMessage);
        Assert.NotNull(provider.LastContext);
        Assert.Same(message, provider.LastContext!.Message);
    }

    /// <summary>
    /// 上下文携带的策略名与实际选中的策略一致
    /// </summary>
    [Fact]
    public async Task DispatchAsync_SetsStrategyNameOnContext()
    {
        var provider = FakeBotProvider.AlwaysSuccess("A");
        var dispatcher = CreateDispatcher(new XiHanBotOptions(), provider);
        var message = new BotMessage { Content = "hi" };
        message.Data[BotMessageDataKeys.Strategy] = BotStrategyNames.Failover;

        await dispatcher.DispatchAsync(message, null, TestContext.Current.CancellationToken);

        Assert.Equal(BotStrategyNames.Failover, provider.LastContext!.StrategyName);
    }

    /// <summary>
    /// 消息里的策略值为空白时退回选项默认策略
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenStrategyValueBlank_UsesDefaultStrategy()
    {
        var first = FakeBotProvider.AlwaysSuccess("A");
        var second = FakeBotProvider.AlwaysSuccess("B");
        var dispatcher = CreateDispatcher(new XiHanBotOptions(), first, second);
        var message = new BotMessage { Content = "hi" };
        message.Data[BotMessageDataKeys.Strategy] = "   ";

        var result = await dispatcher.DispatchAsync(message, null, TestContext.Current.CancellationToken);

        Assert.Null(first.LastContext!.StrategyName);
        Assert.Equal(2, result.Results.Count);
    }

    /// <summary>
    /// 上下文里的渠道列表已经过裁剪与去空白
    /// </summary>
    [Fact]
    public async Task DispatchAsync_NormalizesChannelsOnContext()
    {
        var provider = FakeBotProvider.AlwaysSuccess("A");
        var dispatcher = CreateDispatcher(new XiHanBotOptions(), provider);

        await dispatcher.DispatchAsync(
            new BotMessage { Content = "hi" },
            ["  A  ", "   "],
            TestContext.Current.CancellationToken);

        var channels = provider.LastContext!.Channels;
        Assert.Single(channels);
        Assert.Equal("A", channels[0]);
    }

    private static BotDispatcher CreateDispatcher(XiHanBotOptions options, params IBotProvider[] providers)
    {
        var wrapped = new TestOptionsWrapper<XiHanBotOptions>(options);
        var manager = new BotProviderManager(providers, wrapped);
        return new BotDispatcher(
            manager,
            [],
            [
                new BroadcastStrategy(wrapped, NullLogger<BroadcastStrategy>.Instance),
                new FailoverStrategy(NullLogger<FailoverStrategy>.Instance),
                new PriorityStrategy(NullLogger<PriorityStrategy>.Instance)
            ],
            wrapped,
            NullLogger<BotDispatcher>.Instance);
    }
}
