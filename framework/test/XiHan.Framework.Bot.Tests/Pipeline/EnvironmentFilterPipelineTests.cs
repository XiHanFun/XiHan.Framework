// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Options;
using XiHan.Framework.Bot.Pipeline;

namespace XiHan.Framework.Bot.Tests;

/// <summary>
/// <see cref="EnvironmentFilterPipeline"/> 测试
/// </summary>
/// <remarks>
/// 该管道是"开发环境别把告警发到生产群"的开关。它有三道放行口子：没开开关、白名单为空、拿不到环境名，
/// 三者任一成立都必须放行；只有开关开着、白名单非空、环境名拿得到且不在白名单里，才置 IsSkipped 并短路。
/// </remarks>
public class EnvironmentFilterPipelineTests
{
    /// <summary>
    /// 未启用环境过滤时直接放行
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenFilterDisabled_CallsNext()
    {
        var options = new XiHanBotOptions { EnableEnvironmentFilter = false };
        options.AllowedEnvironments.Add("Production");
        var pipeline = CreatePipeline(options, "Development");
        var context = CreateContext();
        var called = 0;

        await pipeline.InvokeAsync(context, () => { called++; return Task.CompletedTask; });

        Assert.Equal(1, called);
        Assert.False(context.IsSkipped);
    }

    /// <summary>
    /// 白名单为空时直接放行
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenAllowedEnvironmentsEmpty_CallsNext()
    {
        var options = new XiHanBotOptions { EnableEnvironmentFilter = true };
        var pipeline = CreatePipeline(options, "Development");
        var context = CreateContext();
        var called = 0;

        await pipeline.InvokeAsync(context, () => { called++; return Task.CompletedTask; });

        Assert.Equal(1, called);
        Assert.False(context.IsSkipped);
    }

    /// <summary>
    /// 拿不到宿主环境时直接放行（不因为缺环境信息而静默丢消息）
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenEnvironmentUnavailable_CallsNext()
    {
        var options = new XiHanBotOptions { EnableEnvironmentFilter = true };
        options.AllowedEnvironments.Add("Production");
        var pipeline = new EnvironmentFilterPipeline(new TestOptionsWrapper<XiHanBotOptions>(options), null);
        var context = CreateContext();
        var called = 0;

        await pipeline.InvokeAsync(context, () => { called++; return Task.CompletedTask; });

        Assert.Equal(1, called);
        Assert.False(context.IsSkipped);
    }

    /// <summary>
    /// 环境名为空白时直接放行
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenEnvironmentNameBlank_CallsNext()
    {
        var options = new XiHanBotOptions { EnableEnvironmentFilter = true };
        options.AllowedEnvironments.Add("Production");
        var pipeline = CreatePipeline(options, "   ");
        var context = CreateContext();
        var called = 0;

        await pipeline.InvokeAsync(context, () => { called++; return Task.CompletedTask; });

        Assert.Equal(1, called);
        Assert.False(context.IsSkipped);
    }

    /// <summary>
    /// 当前环境在白名单内时放行
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenEnvironmentAllowed_CallsNext()
    {
        var options = new XiHanBotOptions { EnableEnvironmentFilter = true };
        options.AllowedEnvironments.Add("Staging");
        options.AllowedEnvironments.Add("Production");
        var pipeline = CreatePipeline(options, "Production");
        var context = CreateContext();
        var called = 0;

        await pipeline.InvokeAsync(context, () => { called++; return Task.CompletedTask; });

        Assert.Equal(1, called);
        Assert.False(context.IsSkipped);
    }

    /// <summary>
    /// 环境名匹配大小写不敏感
    /// </summary>
    [Fact]
    public async Task InvokeAsync_EnvironmentMatchIsCaseInsensitive()
    {
        var options = new XiHanBotOptions { EnableEnvironmentFilter = true };
        options.AllowedEnvironments.Add("production");
        var pipeline = CreatePipeline(options, "PRODUCTION");
        var context = CreateContext();
        var called = 0;

        await pipeline.InvokeAsync(context, () => { called++; return Task.CompletedTask; });

        Assert.Equal(1, called);
        Assert.False(context.IsSkipped);
    }

    /// <summary>
    /// 当前环境不在白名单内时置跳过标记并短路
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenEnvironmentNotAllowed_SkipsAndShortCircuits()
    {
        var options = new XiHanBotOptions { EnableEnvironmentFilter = true };
        options.AllowedEnvironments.Add("Production");
        var pipeline = CreatePipeline(options, "Development");
        var context = CreateContext();
        var called = 0;

        await pipeline.InvokeAsync(context, () => { called++; return Task.CompletedTask; });

        Assert.Equal(0, called);
        Assert.True(context.IsSkipped);
        Assert.Empty(context.Results);
    }

    private static EnvironmentFilterPipeline CreatePipeline(XiHanBotOptions options, string environmentName)
    {
        return new EnvironmentFilterPipeline(
            new TestOptionsWrapper<XiHanBotOptions>(options),
            new FakeHostEnvironment(environmentName));
    }

    private static BotContext CreateContext()
    {
        return new BotContext(new BotMessage { Content = "hi" }, Array.Empty<string>(), CancellationToken.None);
    }
}
