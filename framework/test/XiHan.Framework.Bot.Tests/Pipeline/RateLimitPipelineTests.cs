// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Options;
using XiHan.Framework.Bot.Pipeline;

namespace XiHan.Framework.Bot.Tests;

/// <summary>
/// <see cref="RateLimitPipeline"/> 测试
/// </summary>
/// <remarks>
/// 限流是滑动一秒窗口内的令牌计数，令牌用尽不是拒绝而是等待——所以"被限流"表现为耗时变长而不是抛异常。
/// 窗口状态挂在实例字段上，每个用例都必须新建管道实例，不能共用。
/// </remarks>
public class RateLimitPipelineTests
{
    /// <summary>
    /// 未启用限流时直接放行
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenRateLimitDisabled_CallsNext()
    {
        var options = new XiHanBotOptions { EnableRateLimitPipeline = false, RateLimitPerSecond = 1 };
        var pipeline = new RateLimitPipeline(new TestOptionsWrapper<XiHanBotOptions>(options));
        var called = 0;

        for (var index = 0; index < 5; index++)
        {
            await pipeline.InvokeAsync(CreateContext(CancellationToken.None), () => { called++; return Task.CompletedTask; });
        }

        Assert.Equal(5, called);
    }

    /// <summary>
    /// 每秒条数不为正时视为不限流
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task InvokeAsync_WhenRateLimitNotPositive_CallsNext(int rateLimit)
    {
        var options = new XiHanBotOptions { EnableRateLimitPipeline = true, RateLimitPerSecond = rateLimit };
        var pipeline = new RateLimitPipeline(new TestOptionsWrapper<XiHanBotOptions>(options));
        var called = 0;

        for (var index = 0; index < 3; index++)
        {
            await pipeline.InvokeAsync(CreateContext(CancellationToken.None), () => { called++; return Task.CompletedTask; });
        }

        Assert.Equal(3, called);
    }

    /// <summary>
    /// 窗口内额度未用尽时不产生等待
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithinQuota_DoesNotWait()
    {
        var options = new XiHanBotOptions { EnableRateLimitPipeline = true, RateLimitPerSecond = 3 };
        var pipeline = new RateLimitPipeline(new TestOptionsWrapper<XiHanBotOptions>(options));
        var called = 0;
        var stopwatch = Stopwatch.StartNew();

        for (var index = 0; index < 3; index++)
        {
            await pipeline.InvokeAsync(CreateContext(TestContext.Current.CancellationToken), () => { called++; return Task.CompletedTask; });
        }

        stopwatch.Stop();

        Assert.Equal(3, called);
        Assert.True(stopwatch.ElapsedMilliseconds < 500, $"额度未用尽却等待了 {stopwatch.ElapsedMilliseconds} 毫秒。");
    }

    /// <summary>
    /// 额度用尽后下一条要等到窗口滑出才放行
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenQuotaExhausted_WaitsForWindow()
    {
        var options = new XiHanBotOptions { EnableRateLimitPipeline = true, RateLimitPerSecond = 1 };
        var pipeline = new RateLimitPipeline(new TestOptionsWrapper<XiHanBotOptions>(options));
        var called = 0;

        await pipeline.InvokeAsync(CreateContext(TestContext.Current.CancellationToken), () => { called++; return Task.CompletedTask; });

        var stopwatch = Stopwatch.StartNew();
        await pipeline.InvokeAsync(CreateContext(TestContext.Current.CancellationToken), () => { called++; return Task.CompletedTask; });
        stopwatch.Stop();

        Assert.Equal(2, called);
        Assert.True(stopwatch.ElapsedMilliseconds >= 500, $"额度已用尽却只等待了 {stopwatch.ElapsedMilliseconds} 毫秒。");
    }

    /// <summary>
    /// 启用限流且令牌已取消时抛出取消异常，不放行下一环节
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenCancelled_Throws()
    {
        var options = new XiHanBotOptions { EnableRateLimitPipeline = true, RateLimitPerSecond = 5 };
        var pipeline = new RateLimitPipeline(new TestOptionsWrapper<XiHanBotOptions>(options));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var called = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pipeline.InvokeAsync(CreateContext(cts.Token), () => { called++; return Task.CompletedTask; }));

        Assert.Equal(0, called);
    }

    /// <summary>
    /// 未启用限流时即便令牌已取消也照常放行（该管道不承担取消检查职责）
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenDisabledAndCancelled_StillCallsNext()
    {
        var options = new XiHanBotOptions { EnableRateLimitPipeline = false, RateLimitPerSecond = 5 };
        var pipeline = new RateLimitPipeline(new TestOptionsWrapper<XiHanBotOptions>(options));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var called = 0;

        await pipeline.InvokeAsync(CreateContext(cts.Token), () => { called++; return Task.CompletedTask; });

        Assert.Equal(1, called);
    }

    private static BotContext CreateContext(CancellationToken cancellationToken)
    {
        return new BotContext(new BotMessage { Content = "hi" }, Array.Empty<string>(), cancellationToken);
    }
}
