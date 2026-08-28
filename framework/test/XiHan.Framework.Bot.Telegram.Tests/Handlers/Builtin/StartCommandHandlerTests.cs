// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Bot.Telegram.Handlers;
using XiHan.Framework.Bot.Telegram.Handlers.Builtin;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.Handlers.Builtin;

/// <summary>
/// <see cref="StartCommandHandler"/> 内置 /start 命令处理器测试
/// </summary>
/// <remarks>
/// /start 是用户与机器人的第一次交互，回复必须稳定：
/// 占位符没替换会让用户看到原始的 {botUsername}；
/// 发送失败必须只记日志（发送门面自带重试），不能把异常抛回分发器触发第二条错误回复。
/// </remarks>
public class StartCommandHandlerTests
{
    /// <summary>
    /// 标注了 /start 命令，且未限定管理员
    /// </summary>
    [Fact]
    public void Attribute_BindsStartCommandForEveryone()
    {
        var attribute = Assert.Single(
            typeof(StartCommandHandler).GetCustomAttributes(typeof(BotCommandAttribute), inherit: false)
                .Cast<BotCommandAttribute>());

        Assert.Equal("/start", attribute.Command);
        Assert.False(attribute.AdminOnly);
        Assert.Empty(attribute.Aliases);
        Assert.False(string.IsNullOrWhiteSpace(attribute.Description));
    }

    /// <summary>
    /// 上下文为空时抛参数空异常
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenContextNull_Throws()
    {
        var handler = CreateHandler(out _, out _);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await handler.HandleAsync(null!, [], TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 尚未回填 Telegram 身份时用机器人名称替换 botUsername 占位符
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenUsernameUnknown_UsesBotName()
    {
        var handler = CreateHandler(out var notifier, out _);
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "main-bot"));
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/start"));

        await handler.HandleAsync(context, [], TestContext.Current.CancellationToken);

        Assert.Single(notifier.SentTexts);
        Assert.Equal("main-bot", notifier.SentTexts[0].BotName);
        Assert.Equal(100L, notifier.SentTexts[0].ChatId);
        Assert.Equal(11, notifier.SentTexts[0].ReplyToMessageId);
        Assert.Contains("main-bot", notifier.SentTexts[0].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("{botUsername}", notifier.SentTexts[0].Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// 已回填 Telegram 用户名时优先用用户名（不含 @）
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenUsernameKnown_UsesTelegramUsername()
    {
        var handler = CreateHandler(out var notifier, out _);
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "main-bot"));
        bot.SetIdentity(123456L, "@my_bot");
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/start"));

        await handler.HandleAsync(context, [], TestContext.Current.CancellationToken);

        Assert.Contains("my_bot", notifier.SentTexts[0].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("main-bot", notifier.SentTexts[0].Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// 回复内容取自平台文案配置，应用层可整体覆盖
    /// </summary>
    [Fact]
    public async Task HandleAsync_UsesConfiguredStartReply()
    {
        var handler = CreateHandler(out var notifier, out var options);
        options.CurrentValue.Texts.StartReply = "欢迎使用 {botUsername}，请先绑定账号。";
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "main-bot"));
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/start"));

        await handler.HandleAsync(context, [], TestContext.Current.CancellationToken);

        Assert.Equal("欢迎使用 main-bot，请先绑定账号。", notifier.SentTexts[0].Text);
    }

    /// <summary>
    /// 定位不到会话时不发送
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenChatIdZero_SendsNothing()
    {
        var handler = CreateHandler(out var notifier, out _);
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateEmptyUpdate());

        await handler.HandleAsync(context, [], TestContext.Current.CancellationToken);

        Assert.Empty(notifier.SentTexts);
    }

    /// <summary>
    /// 欢迎文案被配置为空白时不发送
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenReplyTextBlank_SendsNothing()
    {
        var handler = CreateHandler(out var notifier, out var options);
        options.CurrentValue.Texts.StartReply = "   ";
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/start"));

        await handler.HandleAsync(context, [], TestContext.Current.CancellationToken);

        Assert.Empty(notifier.SentTexts);
    }

    /// <summary>
    /// 发送失败只记日志，不把异常抛回分发器
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenSendFails_SwallowsException()
    {
        var handler = CreateHandler(out var notifier, out _);
        notifier.ExceptionToThrow = new InvalidOperationException("发送失败");
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/start"));

        await handler.HandleAsync(context, [], TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 未被前置消费的深链参数在这里被忽略，仍然回复欢迎文案
    /// </summary>
    [Fact]
    public async Task HandleAsync_IgnoresLeftoverDeepLinkPayload()
    {
        var handler = CreateHandler(out var notifier, out _);
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "main-bot"));
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/start invite-A1"));

        await handler.HandleAsync(context, ["invite-A1"], TestContext.Current.CancellationToken);

        Assert.Single(notifier.SentTexts);
        Assert.Contains("main-bot", notifier.SentTexts[0].Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// 取消令牌透传给发送门面
    /// </summary>
    [Fact]
    public async Task HandleAsync_PassesCancellationTokenToNotifier()
    {
        var handler = CreateHandler(out var notifier, out _);
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/start"));
        using var cts = new CancellationTokenSource();

        await handler.HandleAsync(context, [], cts.Token);

        Assert.Equal(cts.Token, notifier.LastCancellationToken);
    }

    /// <summary>
    /// 构造内置 /start 处理器
    /// </summary>
    /// <param name="notifier">发送门面替身</param>
    /// <param name="options">平台选项监视器</param>
    /// <returns>处理器</returns>
    private static StartCommandHandler CreateHandler(
        out FakeTelegramNotifier notifier,
        out TestOptionsMonitor<TelegramBotPlatformOptions> options)
    {
        notifier = new FakeTelegramNotifier();
        options = TelegramTestFactory.CreatePlatformOptions();
        return new StartCommandHandler(notifier, options, NullLogger<StartCommandHandler>.Instance);
    }
}
