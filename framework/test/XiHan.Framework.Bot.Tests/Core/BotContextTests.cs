// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Tests.Fakes;

namespace XiHan.Framework.Bot.Tests.Core;

/// <summary>
/// <see cref="BotContext"/> 测试
/// </summary>
/// <remarks>
/// 上下文是策略与管道之间唯一的可变共享状态：结果列表、提供者列表、跳过标记都在这里传递。
/// 重点验证 IsSuccess 的"空结果不算成功"语义——重试管道正是靠它判断要不要再来一轮。
/// </remarks>
public class BotContextTests
{
    /// <summary>
    /// 构造后各只读属性保持传入值
    /// </summary>
    [Fact]
    public void Constructor_KeepsMessageChannelsAndToken()
    {
        var message = new BotMessage { Content = "hi" };
        var channels = new[] { "ops" };
        using var cts = new CancellationTokenSource();

        var context = new BotContext(message, channels, cts.Token);

        Assert.Same(message, context.Message);
        Assert.Same(channels, context.Channels);
        Assert.Equal(cts.Token, context.CancellationToken);
        Assert.Empty(context.Providers);
        Assert.Empty(context.Results);
        Assert.Null(context.StrategyName);
        Assert.Null(context.LastException);
        Assert.False(context.IsSkipped);
    }

    /// <summary>
    /// 结果为空时既不算成功也不算存在失败
    /// </summary>
    [Fact]
    public void IsSuccess_WhenNoResults_IsFalse()
    {
        var context = CreateContext();

        Assert.False(context.IsSuccess);
        Assert.False(context.HasFailures);
    }

    /// <summary>
    /// 全部成功时 IsSuccess 为真
    /// </summary>
    [Fact]
    public void IsSuccess_WhenAllResultsSucceeded_IsTrue()
    {
        var context = CreateContext();

        context.AddResult("A", BotResult.Success());
        context.AddResult("B", BotResult.Success());

        Assert.True(context.IsSuccess);
        Assert.False(context.HasFailures);
    }

    /// <summary>
    /// 存在任一失败时 IsSuccess 为假且 HasFailures 为真
    /// </summary>
    [Fact]
    public void IsSuccess_WhenAnyResultFailed_IsFalse()
    {
        var context = CreateContext();

        context.AddResult("A", BotResult.Success());
        context.AddResult("B", BotResult.Failed("boom"));

        Assert.False(context.IsSuccess);
        Assert.True(context.HasFailures);
    }

    /// <summary>
    /// 结果未标注提供者时用传入名补齐
    /// </summary>
    [Fact]
    public void AddResult_WhenProviderMissing_FillsFromArgument()
    {
        var context = CreateContext();

        context.AddResult("DingTalk", BotResult.Success());

        Assert.Equal("DingTalk", context.Results[0].Provider);
    }

    /// <summary>
    /// 结果已标注提供者时保留原值
    /// </summary>
    [Fact]
    public void AddResult_WhenProviderPresent_KeepsOriginal()
    {
        var context = CreateContext();

        context.AddResult("DingTalk", BotResult.Success(provider: "Lark"));

        Assert.Equal("Lark", context.Results[0].Provider);
    }

    /// <summary>
    /// 结果为 null 时抛出参数空异常
    /// </summary>
    [Fact]
    public void AddResult_WhenResultNull_Throws()
    {
        var context = CreateContext();

        Assert.Throws<ArgumentNullException>(() => context.AddResult("A", null!));
    }

    /// <summary>
    /// 清空结果后回到"无结果"状态
    /// </summary>
    [Fact]
    public void ClearResults_ResetsToEmpty()
    {
        var context = CreateContext();
        context.AddResult("A", BotResult.Success());

        context.ClearResults();

        Assert.Empty(context.Results);
        Assert.False(context.IsSuccess);
    }

    /// <summary>
    /// 设置提供者后按传入顺序暴露
    /// </summary>
    [Fact]
    public void SetProviders_KeepsOrder()
    {
        var context = CreateContext();
        var first = FakeBotProvider.AlwaysSuccess("A");
        var second = FakeBotProvider.AlwaysSuccess("B");

        context.SetProviders([first, second]);

        Assert.Equal(2, context.Providers.Count);
        Assert.Same(first, context.Providers[0]);
        Assert.Same(second, context.Providers[1]);
    }

    /// <summary>
    /// 提供者列表为 null 时退化为空列表而不是抛出
    /// </summary>
    [Fact]
    public void SetProviders_WhenNull_BecomesEmpty()
    {
        var context = CreateContext();
        context.SetProviders([FakeBotProvider.AlwaysSuccess("A")]);

        context.SetProviders(null!);

        Assert.Empty(context.Providers);
    }

    /// <summary>
    /// 设置提供者时做快照，后续改动源集合不影响上下文
    /// </summary>
    [Fact]
    public void SetProviders_TakesSnapshot()
    {
        var context = CreateContext();
        var providers = new List<IBotProvider> { FakeBotProvider.AlwaysSuccess("A") };

        context.SetProviders(providers);
        providers.Add(FakeBotProvider.AlwaysSuccess("B"));

        Assert.Single(context.Providers);
    }

    /// <summary>
    /// 上下文项的键名大小写不敏感
    /// </summary>
    [Fact]
    public void Items_KeyLookupIsCaseInsensitive()
    {
        var context = CreateContext();

        context.Items["TraceId"] = "abc";

        Assert.True(context.Items.ContainsKey("traceid"));
        Assert.Equal("abc", context.Items["TRACEID"]);
    }

    private static BotContext CreateContext()
    {
        return new BotContext(new BotMessage { Content = "hi" }, [], CancellationToken.None);
    }
}
