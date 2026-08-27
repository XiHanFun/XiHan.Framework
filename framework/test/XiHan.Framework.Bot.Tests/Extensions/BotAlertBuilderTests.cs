// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Enums;
using XiHan.Framework.Bot.Extensions;

namespace XiHan.Framework.Bot.Tests;

/// <summary>
/// <see cref="BotAlertBuilder"/> 测试
/// </summary>
/// <remarks>
/// 构建器的关键契约有两条：每个配置方法都返回自身以便链式书写；
/// 没调用 SendTo 时必须走广播重载而不是传一个空渠道列表下去——后者会被解析成"一个提供者都不匹配"。
/// </remarks>
public class BotAlertBuilderTests
{
    /// <summary>
    /// 所有配置方法都返回同一个构建器实例
    /// </summary>
    [Fact]
    public void FluentMethods_ReturnSameInstance()
    {
        var builder = new BotAlertBuilder(new FakeBotClient());

        Assert.Same(builder, builder.Title("t"));
        Assert.Same(builder, builder.Content("c"));
        Assert.Same(builder, builder.Type(BotMessageType.Markdown));
        Assert.Same(builder, builder.Mention("a"));
        Assert.Same(builder, builder.SendTo("ops"));
    }

    /// <summary>
    /// 配置项按序写进消息体
    /// </summary>
    [Fact]
    public async Task SendAsync_CarriesConfiguredMessage()
    {
        var client = new FakeBotClient();

        await new BotAlertBuilder(client)
            .Title("磁盘告警")
            .Content("使用率 91%")
            .Type(BotMessageType.Markdown)
            .Mention("ops", "dba")
            .SendAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(client.LastMessage);
        Assert.Equal("磁盘告警", client.LastMessage!.Title);
        Assert.Equal("使用率 91%", client.LastMessage.Content);
        Assert.Equal(BotMessageType.Markdown, client.LastMessage.Type);
        Assert.Equal(2, client.LastMessage.Mentions.Count);
        Assert.Contains("ops", client.LastMessage.Mentions);
        Assert.Contains("dba", client.LastMessage.Mentions);
    }

    /// <summary>
    /// 多次调用提及会追加而不是覆盖
    /// </summary>
    [Fact]
    public async Task Mention_CalledTwice_Appends()
    {
        var client = new FakeBotClient();

        await new BotAlertBuilder(client)
            .Content("c")
            .Mention("a")
            .Mention("b", "c")
            .SendAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, client.LastMessage!.Mentions.Count);
    }

    /// <summary>
    /// 未指定渠道时走广播重载
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenNoChannel_UsesBroadcastOverload()
    {
        var client = new FakeBotClient();

        await new BotAlertBuilder(client).Content("c").SendAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, client.SendCount);
        Assert.False(client.UsedChannelOverload);
        Assert.Null(client.LastChannels);
    }

    /// <summary>
    /// 指定渠道时走带渠道的重载并把渠道原样传下去
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenChannelSpecified_UsesChannelOverload()
    {
        var client = new FakeBotClient();

        await new BotAlertBuilder(client)
            .Content("c")
            .SendTo("ops", "sre")
            .SendAsync(TestContext.Current.CancellationToken);

        Assert.True(client.UsedChannelOverload);
        Assert.NotNull(client.LastChannels);
        Assert.Equal(2, client.LastChannels!.Count);
        Assert.Equal("ops", client.LastChannels[0]);
        Assert.Equal("sre", client.LastChannels[1]);
    }

    /// <summary>
    /// 多次调用 SendTo 会追加渠道
    /// </summary>
    [Fact]
    public async Task SendTo_CalledTwice_Appends()
    {
        var client = new FakeBotClient();

        await new BotAlertBuilder(client)
            .Content("c")
            .SendTo("ops")
            .SendTo("sre")
            .SendAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, client.LastChannels!.Count);
    }

    /// <summary>
    /// 什么都不配置也能发送，消息为默认的空文本
    /// </summary>
    [Fact]
    public async Task SendAsync_WithoutConfiguration_SendsDefaultMessage()
    {
        var client = new FakeBotClient();

        await new BotAlertBuilder(client).SendAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(client.LastMessage);
        Assert.Null(client.LastMessage!.Title);
        Assert.Equal(string.Empty, client.LastMessage.Content);
        Assert.Equal(BotMessageType.Text, client.LastMessage.Type);
        Assert.Empty(client.LastMessage.Mentions);
    }

    /// <summary>
    /// 构建器返回的是客户端给出的聚合结果
    /// </summary>
    [Fact]
    public async Task SendAsync_ReturnsClientResult()
    {
        var client = new FakeBotClient();

        var result = await new BotAlertBuilder(client).Content("c").SendAsync(TestContext.Current.CancellationToken);

        Assert.Same(client.Result, result);
    }
}
