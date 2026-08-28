// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Options;
using XiHan.Framework.Bot.Pipeline;
using XiHan.Framework.Bot.Providers;

namespace XiHan.Framework.Bot.Tests;

/// <summary>
/// <see cref="RetryPipeline"/> 测试
/// </summary>
/// <remarks>
/// 重试次数是"总尝试次数"而不是"额外补偿次数"：RetryCount=3 表示最多执行三轮，不是执行四轮。
/// 每轮开始都会清空结果，所以最终 Results 只保留最后一轮；下一轮只重投上一轮失败的提供者。
/// 所有用例把 RetryDelay 设为零，避免真实等待拖慢测试。
/// </remarks>
public class RetryPipelineTests
{
    /// <summary>
    /// 未启用重试时只执行一次
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenRetryDisabled_InvokesNextOnce()
    {
        var options = new XiHanBotOptions { EnableRetryPipeline = false, RetryCount = 5, RetryDelay = TimeSpan.Zero };
        var context = CreateContext();
        var attempts = 0;

        await CreatePipeline(options).InvokeAsync(context, () =>
        {
            attempts++;
            context.AddResult("A", BotResult.Failed("down"));
            return Task.CompletedTask;
        });

        Assert.Equal(1, attempts);
    }

    /// <summary>
    /// 重试次数不大于 1 时只执行一次
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    public async Task InvokeAsync_WhenRetryCountNotGreaterThanOne_InvokesNextOnce(int retryCount)
    {
        var options = new XiHanBotOptions { EnableRetryPipeline = true, RetryCount = retryCount, RetryDelay = TimeSpan.Zero };
        var context = CreateContext();
        var attempts = 0;

        await CreatePipeline(options).InvokeAsync(context, () =>
        {
            attempts++;
            context.AddResult("A", BotResult.Failed("down"));
            return Task.CompletedTask;
        });

        Assert.Equal(1, attempts);
    }

    /// <summary>
    /// 一直失败时按 RetryCount 执行满且不再多跑一轮
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenAlwaysFails_InvokesNextExactlyRetryCountTimes()
    {
        var options = new XiHanBotOptions { EnableRetryPipeline = true, RetryCount = 3, RetryDelay = TimeSpan.Zero };
        var context = CreateContext();
        var attempts = 0;

        await CreatePipeline(options).InvokeAsync(context, () =>
        {
            attempts++;
            context.AddResult("A", BotResult.Failed("down"));
            return Task.CompletedTask;
        });

        Assert.Equal(3, attempts);
        Assert.Single(context.Results);
        Assert.False(context.IsSuccess);
    }

    /// <summary>
    /// 中途成功后立即停止重试
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenSucceedsOnSecondAttempt_StopsRetrying()
    {
        var options = new XiHanBotOptions { EnableRetryPipeline = true, RetryCount = 5, RetryDelay = TimeSpan.Zero };
        var context = CreateContext();
        var attempts = 0;

        await CreatePipeline(options).InvokeAsync(context, () =>
        {
            attempts++;
            context.AddResult("A", attempts >= 2 ? BotResult.Success() : BotResult.Failed("down"));
            return Task.CompletedTask;
        });

        Assert.Equal(2, attempts);
        Assert.Single(context.Results);
        Assert.True(context.IsSuccess);
    }

    /// <summary>
    /// 每轮开始清空结果，最终只保留最后一轮的明细
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ClearsResultsBetweenAttempts()
    {
        var options = new XiHanBotOptions { EnableRetryPipeline = true, RetryCount = 3, RetryDelay = TimeSpan.Zero };
        var context = CreateContext();
        var attempts = 0;

        await CreatePipeline(options).InvokeAsync(context, () =>
        {
            attempts++;
            context.AddResult("A", BotResult.Failed($"attempt-{attempts}"));
            return Task.CompletedTask;
        });

        Assert.Single(context.Results);
        Assert.Equal("attempt-3", context.Results[0].Message);
    }

    /// <summary>
    /// 下一轮只重投上一轮失败的提供者
    /// </summary>
    [Fact]
    public async Task InvokeAsync_RetriesOnlyFailedProviders()
    {
        var options = new XiHanBotOptions { EnableRetryPipeline = true, RetryCount = 2, RetryDelay = TimeSpan.Zero };
        var healthy = FakeBotProvider.AlwaysSuccess("A");
        var failing = FakeBotProvider.AlwaysFailed("B", "down");
        var context = CreateContext();
        context.SetProviders([healthy, failing]);
        var observed = new List<string>();

        await CreatePipeline(options).InvokeAsync(context, () =>
        {
            observed.Add(string.Join(",", context.Providers.Select(provider => provider.Name)));
            foreach (var provider in context.Providers)
            {
                context.AddResult(
                    provider.Name,
                    provider.Name == "A" ? BotResult.Success(provider: "A") : BotResult.Failed("down", "B"));
            }

            return Task.CompletedTask;
        });

        Assert.Equal(2, observed.Count);
        Assert.Equal("A,B", observed[0]);
        Assert.Equal("B", observed[1]);
    }

    /// <summary>
    /// 上一轮没有留下带名失败明细时，下一轮恢复为原始提供者集合
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenNoNamedFailure_RestoresOriginalProviders()
    {
        var options = new XiHanBotOptions { EnableRetryPipeline = true, RetryCount = 2, RetryDelay = TimeSpan.Zero };
        var first = FakeBotProvider.AlwaysSuccess("A");
        var second = FakeBotProvider.AlwaysSuccess("B");
        var context = CreateContext();
        context.SetProviders([first, second]);
        var observed = new List<string>();

        await CreatePipeline(options).InvokeAsync(context, () =>
        {
            observed.Add(string.Join(",", context.Providers.Select(provider => provider.Name)));
            return Task.CompletedTask;
        });

        Assert.Equal(2, observed.Count);
        Assert.Equal("A,B", observed[0]);
        Assert.Equal("A,B", observed[1]);
    }

    /// <summary>
    /// 内层一直抛异常时，最后一轮把异常原样抛出
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenNextAlwaysThrows_RethrowsAfterLastAttempt()
    {
        var options = new XiHanBotOptions { EnableRetryPipeline = true, RetryCount = 2, RetryDelay = TimeSpan.Zero };
        var context = CreateContext();
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreatePipeline(options).InvokeAsync(context, () =>
            {
                attempts++;
                throw new InvalidOperationException("boom");
            }));

        Assert.Equal(2, attempts);
        Assert.Equal("boom", exception.Message);
    }

    /// <summary>
    /// 首轮抛异常但次轮成功时不再抛出
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenNextThrowsThenSucceeds_DoesNotThrow()
    {
        var options = new XiHanBotOptions { EnableRetryPipeline = true, RetryCount = 3, RetryDelay = TimeSpan.Zero };
        var context = CreateContext();
        var attempts = 0;

        await CreatePipeline(options).InvokeAsync(context, () =>
        {
            attempts++;
            if (attempts == 1)
            {
                throw new InvalidOperationException("boom");
            }

            context.AddResult("A", BotResult.Success());
            return Task.CompletedTask;
        });

        Assert.Equal(2, attempts);
        Assert.True(context.IsSuccess);
        Assert.Null(context.LastException);
    }

    /// <summary>
    /// 内层完全不产出结果时按失败处理并跑满重试次数
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenNoResultProduced_KeepsRetrying()
    {
        var options = new XiHanBotOptions { EnableRetryPipeline = true, RetryCount = 3, RetryDelay = TimeSpan.Zero };
        var context = CreateContext();
        var attempts = 0;

        await CreatePipeline(options).InvokeAsync(context, () =>
        {
            attempts++;
            return Task.CompletedTask;
        });

        Assert.Equal(3, attempts);
        Assert.Empty(context.Results);
    }

    /// <summary>
    /// 重试间隔期间令牌被取消时抛出取消异常
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenCancelledDuringDelay_Throws()
    {
        var options = new XiHanBotOptions { EnableRetryPipeline = true, RetryCount = 3, RetryDelay = TimeSpan.FromSeconds(30) };
        using var cts = new CancellationTokenSource();
        var context = new BotContext(new BotMessage { Content = "hi" }, [], cts.Token);
        var attempts = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreatePipeline(options).InvokeAsync(context, async () =>
            {
                attempts++;
                context.AddResult("A", BotResult.Failed("down"));
                await cts.CancelAsync();
            }));

        Assert.Equal(1, attempts);
    }

    private static RetryPipeline CreatePipeline(XiHanBotOptions options)
    {
        return new RetryPipeline(new TestOptionsWrapper<XiHanBotOptions>(options), NullLogger<RetryPipeline>.Instance);
    }

    private static BotContext CreateContext()
    {
        return new BotContext(new BotMessage { Content = "hi" }, [], CancellationToken.None);
    }
}
