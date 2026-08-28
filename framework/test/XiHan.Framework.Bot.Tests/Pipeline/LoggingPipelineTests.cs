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
/// <see cref="LoggingPipeline"/> 测试
/// </summary>
/// <remarks>
/// 日志管道本身不改变调度语义，所以断言集中在"无论开关如何都必须放行"和"异常必须原样上抛"这两点。
/// 具体写了几条日志属于实现细节，不做断言。
/// </remarks>
public class LoggingPipelineTests
{
    /// <summary>
    /// 未启用日志管道时直接放行
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenLoggingDisabled_CallsNext()
    {
        var options = new XiHanBotOptions { EnableLoggingPipeline = false };
        var context = CreateContext();
        var called = 0;

        await CreatePipeline(options).InvokeAsync(context, () => { called++; return Task.CompletedTask; });

        Assert.Equal(1, called);
    }

    /// <summary>
    /// 启用日志管道时同样放行，且不改动结果
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenLoggingEnabled_CallsNextAndKeepsResults()
    {
        var options = new XiHanBotOptions { EnableLoggingPipeline = true };
        var context = CreateContext();
        context.SetProviders([FakeBotProvider.AlwaysSuccess("A")]);
        var called = 0;

        await CreatePipeline(options).InvokeAsync(context, () =>
        {
            called++;
            context.AddResult("A", BotResult.Success());
            return Task.CompletedTask;
        });

        Assert.Equal(1, called);
        Assert.Single(context.Results);
        Assert.True(context.IsSuccess);
    }

    /// <summary>
    /// 内层抛异常时记录后原样上抛
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenNextThrows_Rethrows()
    {
        var options = new XiHanBotOptions { EnableLoggingPipeline = true };
        var context = CreateContext();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreatePipeline(options).InvokeAsync(context, () => throw new InvalidOperationException("boom")));

        Assert.Equal("boom", exception.Message);
    }

    /// <summary>
    /// 无结果且被跳过时不抛异常，正常收尾
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenSkippedWithNoResult_CompletesNormally()
    {
        var options = new XiHanBotOptions { EnableLoggingPipeline = true };
        var context = CreateContext();

        await CreatePipeline(options).InvokeAsync(context, () =>
        {
            context.IsSkipped = true;
            return Task.CompletedTask;
        });

        Assert.True(context.IsSkipped);
        Assert.Empty(context.Results);
    }

    /// <summary>
    /// 结果里既有成功又有失败时同样正常收尾
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WithMixedResults_CompletesNormally()
    {
        var options = new XiHanBotOptions { EnableLoggingPipeline = true };
        var context = CreateContext();

        await CreatePipeline(options).InvokeAsync(context, () =>
        {
            context.AddResult("A", BotResult.Success());
            context.AddResult("B", BotResult.Failed("down"));
            return Task.CompletedTask;
        });

        Assert.Equal(2, context.Results.Count);
        Assert.True(context.HasFailures);
    }

    private static LoggingPipeline CreatePipeline(XiHanBotOptions options)
    {
        return new LoggingPipeline(new TestOptionsWrapper<XiHanBotOptions>(options), NullLogger<LoggingPipeline>.Instance);
    }

    private static BotContext CreateContext()
    {
        return new BotContext(new BotMessage { Content = "hi" }, [], CancellationToken.None);
    }
}
