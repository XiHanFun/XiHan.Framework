// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Enums;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Telegram.Messaging;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.Messaging;

/// <summary>
/// <see cref="TelegramBotProvider"/> 单发通道提供者测试
/// </summary>
/// <remarks>
/// 提供者只有在参数齐备时才会构造 Bot 客户端并真的发消息，
/// 所以这里只覆盖「构造客户端之前」的全部 fail-closed 分支：未配置 / 已禁用 / 缺 Token / 缺会话 Id。
/// 真正发送的分支需要连 Telegram，按外部依赖不实连的原则不在单测覆盖范围内。
/// </remarks>
public class TelegramBotProviderTests
{
    /// <summary>
    /// 提供者名称固定为 Telegram，与 BotProviderNames 常量一致
    /// </summary>
    [Fact]
    public void Name_IsTelegramProviderName()
    {
        var provider = new TelegramBotProvider(new FakeTelegramConfigStore());

        Assert.Equal(BotProviderNames.Telegram, provider.Name);
        Assert.Equal("Telegram", provider.Name);
    }

    /// <summary>
    /// 提供者实现 IBotProvider，可被 Bot 调度器统一编排
    /// </summary>
    [Fact]
    public void Type_ImplementsBotProviderAbstraction()
    {
        Assert.IsAssignableFrom<IBotProvider>(new TelegramBotProvider(new FakeTelegramConfigStore()));
    }

    /// <summary>
    /// 未配置时 fail-closed 返回 BadRequest，不做任何发送尝试
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenNotConfigured_ReturnsBadRequest()
    {
        var provider = new TelegramBotProvider(new FakeTelegramConfigStore(null));
        var message = CreateMessage();

        var result = await provider.SendAsync(message, CreateContext(message));

        Assert.Equal(BotResultCodes.BadRequest, result.Code);
        Assert.Equal("Telegram provider is not configured or disabled.", result.Message);
        Assert.Equal(BotProviderNames.Telegram, result.Provider);
    }

    /// <summary>
    /// 配置已禁用时同样 fail-closed
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenDisabled_ReturnsBadRequest()
    {
        var options = new TelegramOptions
        {
            Enabled = false,
            Token = "123456:AAHfake-telegram-token",
            ChatId = "100"
        };
        var provider = new TelegramBotProvider(new FakeTelegramConfigStore(options));
        var message = CreateMessage();

        var result = await provider.SendAsync(message, CreateContext(message));

        Assert.Equal(BotResultCodes.BadRequest, result.Code);
        Assert.Equal("Telegram provider is not configured or disabled.", result.Message);
    }

    /// <summary>
    /// 缺少 Token 时返回 BadRequest 并指明原因
    /// </summary>
    /// <param name="token">Bot 令牌</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendAsync_WhenTokenBlank_ReturnsBadRequest(string token)
    {
        var options = new TelegramOptions { Enabled = true, Token = token, ChatId = "100" };
        var provider = new TelegramBotProvider(new FakeTelegramConfigStore(options));
        var message = CreateMessage();

        var result = await provider.SendAsync(message, CreateContext(message));

        Assert.Equal(BotResultCodes.BadRequest, result.Code);
        Assert.Equal("Telegram token is required.", result.Message);
        Assert.Equal(BotProviderNames.Telegram, result.Provider);
    }

    /// <summary>
    /// 既没有默认会话 Id 也没有消息级覆盖时返回 BadRequest
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenChatIdMissing_ReturnsBadRequest()
    {
        var options = new TelegramOptions { Enabled = true, Token = "123456:AAHfake-telegram-token" };
        var provider = new TelegramBotProvider(new FakeTelegramConfigStore(options));
        var message = CreateMessage();

        var result = await provider.SendAsync(message, CreateContext(message));

        Assert.Equal(BotResultCodes.BadRequest, result.Code);
        Assert.Equal("Telegram chat id is required.", result.Message);
        Assert.Equal(BotProviderNames.Telegram, result.Provider);
    }

    /// <summary>
    /// 消息级会话 Id 为空白时不覆盖配置里的空值，仍然 fail-closed
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenChatIdOverrideBlank_StillReturnsBadRequest()
    {
        var options = new TelegramOptions { Enabled = true, Token = "123456:AAHfake-telegram-token" };
        var provider = new TelegramBotProvider(new FakeTelegramConfigStore(options));
        var message = CreateMessage();
        message.Data[TelegramMessageDataKeys.TelegramChatId] = "   ";

        var result = await provider.SendAsync(message, CreateContext(message));

        Assert.Equal(BotResultCodes.BadRequest, result.Code);
        Assert.Equal("Telegram chat id is required.", result.Message);
    }

    /// <summary>
    /// 配置读取带上下文的取消令牌，调度侧取消能一路传下去
    /// </summary>
    [Fact]
    public async Task SendAsync_PassesContextCancellationTokenToConfigStore()
    {
        var store = new FakeTelegramConfigStore(null);
        var provider = new TelegramBotProvider(store);
        var message = CreateMessage();
        using var cts = new CancellationTokenSource();

        await provider.SendAsync(message, CreateContext(message, cts.Token));

        Assert.Equal(1, store.GetCount);
        Assert.Equal(cts.Token, store.LastCancellationToken);
    }

    /// <summary>
    /// 每次发送都重新读取配置，应用层热更新配置即时生效
    /// </summary>
    [Fact]
    public async Task SendAsync_ReadsConfigOnEverySend()
    {
        var store = new FakeTelegramConfigStore(null);
        var provider = new TelegramBotProvider(store);
        var message = CreateMessage();

        await provider.SendAsync(message, CreateContext(message));
        await provider.SendAsync(message, CreateContext(message));

        Assert.Equal(2, store.GetCount);
    }

    /// <summary>
    /// 构造一条最简消息
    /// </summary>
    /// <returns>Bot 消息</returns>
    private static BotMessage CreateMessage()
    {
        return new BotMessage
        {
            Title = "标题",
            Content = "正文",
            Type = BotMessageType.Text
        };
    }

    /// <summary>
    /// 构造调度上下文
    /// </summary>
    /// <param name="message">Bot 消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>调度上下文</returns>
    private static BotContext CreateContext(BotMessage message, CancellationToken cancellationToken = default)
    {
        return new BotContext(message, [BotProviderNames.Telegram], cancellationToken);
    }
}
