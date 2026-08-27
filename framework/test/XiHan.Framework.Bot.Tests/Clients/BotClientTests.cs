// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Bot.Clients;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Options;
using XiHan.Framework.Bot.Pipeline;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Strategy;

namespace XiHan.Framework.Bot.Tests;

/// <summary>
/// <see cref="BotClient"/> 测试
/// </summary>
/// <remarks>
/// 客户端是一层薄封装，值得验证的是参数校验、取消令牌在批量/延迟场景下的传导，
/// 以及模板发送"先渲染再调度"的顺序。所有提供者都是手写替身，不发起任何网络请求。
/// </remarks>
public class BotClientTests
{
    /// <summary>
    /// 广播重载在消息为 null 时抛出参数空异常
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenMessageNull_Throws()
    {
        var client = CreateClient(out _, FakeBotProvider.AlwaysSuccess("A"));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.SendAsync(null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 指定渠道的重载在消息为 null 时抛出参数空异常
    /// </summary>
    [Fact]
    public async Task SendAsync_WithChannels_WhenMessageNull_Throws()
    {
        var client = CreateClient(out _, FakeBotProvider.AlwaysSuccess("A"));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.SendAsync(null!, new[] { "A" }, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 广播发送把消息交给全部提供者
    /// </summary>
    [Fact]
    public async Task SendAsync_BroadcastsToAllProviders()
    {
        var first = FakeBotProvider.AlwaysSuccess("A");
        var second = FakeBotProvider.AlwaysSuccess("B");
        var client = CreateClient(out _, first, second);
        var message = new BotMessage { Content = "hi" };

        var result = await client.SendAsync(message, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Results.Count);
        Assert.Same(message, first.LastMessage);
        Assert.Same(message, second.LastMessage);
    }

    /// <summary>
    /// 指定渠道时只有命中的提供者收到消息
    /// </summary>
    [Fact]
    public async Task SendAsync_WithChannels_OnlyMatchedProvidersReceive()
    {
        var first = FakeBotProvider.AlwaysSuccess("A");
        var second = FakeBotProvider.AlwaysSuccess("B");
        var client = CreateClient(out _, first, second);

        var result = await client.SendAsync(new BotMessage { Content = "hi" }, new[] { "A" }, TestContext.Current.CancellationToken);

        Assert.Single(result.Results);
        Assert.Equal(1, first.CallCount);
        Assert.Equal(0, second.CallCount);
    }

    /// <summary>
    /// 渠道为 null 时等价于广播
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenChannelsNull_Broadcasts()
    {
        var first = FakeBotProvider.AlwaysSuccess("A");
        var second = FakeBotProvider.AlwaysSuccess("B");
        var client = CreateClient(out _, first, second);

        var result = await client.SendAsync(new BotMessage { Content = "hi" }, null, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Results.Count);
    }

    /// <summary>
    /// 模板发送先渲染再调度，渲染结果就是投递出去的消息
    /// </summary>
    [Fact]
    public async Task SendTemplateAsync_RendersThenDispatches()
    {
        var provider = FakeBotProvider.AlwaysSuccess("A");
        var client = CreateClient(out var templateEngine, provider);
        var rendered = new BotMessage { Content = "已渲染" };
        templateEngine.Message = rendered;
        var model = new { Level = "严重" };

        var result = await client.SendTemplateAsync("alert", model, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("alert", templateEngine.LastTemplateName);
        Assert.Same(model, templateEngine.LastModel);
        Assert.Same(rendered, provider.LastMessage);
    }

    /// <summary>
    /// 模板发送在令牌已取消时先抛出，不触发渲染
    /// </summary>
    [Fact]
    public async Task SendTemplateAsync_WhenCancelled_ThrowsBeforeRendering()
    {
        var client = CreateClient(out var templateEngine, FakeBotProvider.AlwaysSuccess("A"));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => client.SendTemplateAsync("alert", null, null, cts.Token));

        Assert.Equal(0, templateEngine.RenderCount);
    }

    /// <summary>
    /// 批量发送逐条投递并按顺序返回等量结果
    /// </summary>
    [Fact]
    public async Task SendBatchAsync_ReturnsOneResultPerMessage()
    {
        var provider = FakeBotProvider.AlwaysSuccess("A");
        var client = CreateClient(out _, provider);
        var messages = new[]
        {
            new BotMessage { Content = "第一条" },
            new BotMessage { Content = "第二条" },
            new BotMessage { Content = "第三条" }
        };

        var results = await client.SendBatchAsync(messages, null, TestContext.Current.CancellationToken);

        Assert.Equal(3, results.Count);
        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(3, provider.CallCount);
        Assert.Same(messages[2], provider.LastMessage);
    }

    /// <summary>
    /// 批量发送在消息集合为 null 时抛出参数空异常
    /// </summary>
    [Fact]
    public async Task SendBatchAsync_WhenMessagesNull_Throws()
    {
        var client = CreateClient(out _, FakeBotProvider.AlwaysSuccess("A"));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.SendBatchAsync(null!, null, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 批量发送为空集合时返回空结果且不触碰提供者
    /// </summary>
    [Fact]
    public async Task SendBatchAsync_WhenEmpty_ReturnsEmpty()
    {
        var provider = FakeBotProvider.AlwaysSuccess("A");
        var client = CreateClient(out _, provider);

        var results = await client.SendBatchAsync(Array.Empty<BotMessage>(), null, TestContext.Current.CancellationToken);

        Assert.Empty(results);
        Assert.Equal(0, provider.CallCount);
    }

    /// <summary>
    /// 批量发送在令牌已取消时立即抛出且一条都不发
    /// </summary>
    [Fact]
    public async Task SendBatchAsync_WhenCancelled_ThrowsWithoutSending()
    {
        var provider = FakeBotProvider.AlwaysSuccess("A");
        var client = CreateClient(out _, provider);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => client.SendBatchAsync(new[] { new BotMessage { Content = "hi" } }, null, cts.Token));

        Assert.Equal(0, provider.CallCount);
    }

    /// <summary>
    /// 延迟发送在消息为 null 时抛出参数空异常
    /// </summary>
    [Fact]
    public async Task SendDelayedAsync_WhenMessageNull_Throws()
    {
        var client = CreateClient(out _, FakeBotProvider.AlwaysSuccess("A"));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.SendDelayedAsync(null!, TimeSpan.Zero, null, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 延迟为零或负数时不等待，直接发送
    /// </summary>
    [Fact]
    public async Task SendDelayedAsync_WhenDelayNotPositive_SendsImmediately()
    {
        var provider = FakeBotProvider.AlwaysSuccess("A");
        var client = CreateClient(out _, provider);
        var stopwatch = Stopwatch.StartNew();

        await client.SendDelayedAsync(new BotMessage { Content = "hi" }, TimeSpan.Zero, null, TestContext.Current.CancellationToken);
        await client.SendDelayedAsync(new BotMessage { Content = "hi" }, TimeSpan.FromSeconds(-1), null, TestContext.Current.CancellationToken);

        stopwatch.Stop();

        Assert.Equal(2, provider.CallCount);
        Assert.True(stopwatch.ElapsedMilliseconds < 500, $"零延迟却等待了 {stopwatch.ElapsedMilliseconds} 毫秒。");
    }

    /// <summary>
    /// 延迟发送在等待期间被取消时抛出且不发送
    /// </summary>
    [Fact]
    public async Task SendDelayedAsync_WhenCancelledDuringDelay_Throws()
    {
        var provider = FakeBotProvider.AlwaysSuccess("A");
        var client = CreateClient(out _, provider);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.SendDelayedAsync(new BotMessage { Content = "hi" }, TimeSpan.FromSeconds(30), null, cts.Token));

        Assert.Equal(0, provider.CallCount);
    }

    private static BotClient CreateClient(out FakeBotTemplateEngine templateEngine, params IBotProvider[] providers)
    {
        templateEngine = new FakeBotTemplateEngine();
        var options = new XiHanBotOptions();
        var wrapped = new TestOptionsWrapper<XiHanBotOptions>(options);
        var manager = new BotProviderManager(providers, wrapped);
        var dispatcher = new BotDispatcher(
            manager,
            Array.Empty<IBotPipeline>(),
            new IBotStrategy[] { new BroadcastStrategy(wrapped, NullLogger<BroadcastStrategy>.Instance) },
            wrapped,
            NullLogger<BotDispatcher>.Instance);

        return new BotClient(dispatcher, templateEngine);
    }
}
