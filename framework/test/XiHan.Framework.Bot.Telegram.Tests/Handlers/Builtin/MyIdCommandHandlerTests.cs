// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Bot.Telegram.Handlers;
using XiHan.Framework.Bot.Telegram.Handlers.Builtin;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.Handlers.Builtin;

/// <summary>
/// <see cref="MyIdCommandHandler"/> 内置 /myid 命令处理器测试
/// </summary>
/// <remarks>
/// /myid 是配置群组白名单与管理员列表时唯一的取数入口（fail-closed 语义下拿不到 ChatId 就没法开通），
/// 因此 ChatId / UserId 的替换必须用固定文化格式化——跟随当前区域会让大负数 ChatId 被加上千分位分隔符，
/// 复制到配置里就成了一个无效值。
/// </remarks>
public class MyIdCommandHandlerTests
{
    /// <summary>
    /// 标注了 /myid 命令与 /id 别名，且未限定管理员
    /// </summary>
    [Fact]
    public void Attribute_BindsMyIdCommandWithShortAlias()
    {
        var attribute = Assert.Single(
            typeof(MyIdCommandHandler).GetCustomAttributes(typeof(BotCommandAttribute), inherit: false)
                .Cast<BotCommandAttribute>());

        Assert.Equal("/myid", attribute.Command);
        Assert.False(attribute.AdminOnly);
        Assert.Equal(new[] { "/id" }, attribute.GetNormalizedAliases());
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
    /// 同时替换 chatId 与 userId 两个占位符
    /// </summary>
    [Fact]
    public async Task HandleAsync_ReplacesChatIdAndUserIdPlaceholders()
    {
        var handler = CreateHandler(out var notifier, out _);
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/myid", chatId: 100, userId: 200));

        await handler.HandleAsync(context, [], TestContext.Current.CancellationToken);

        Assert.Equal(1, notifier.SentTexts.Count);
        Assert.Contains("100", notifier.SentTexts[0].Text, StringComparison.Ordinal);
        Assert.Contains("200", notifier.SentTexts[0].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("{chatId}", notifier.SentTexts[0].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("{userId}", notifier.SentTexts[0].Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// 群组的负数 ChatId 按固定文化原样输出，不带千分位分隔符
    /// </summary>
    [Fact]
    public async Task HandleAsync_FormatsLargeNegativeChatIdWithoutGroupSeparators()
    {
        var handler = CreateHandler(out var notifier, out var options);
        options.CurrentValue.Texts.MyIdReply = "{chatId}|{userId}";
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(
            bot,
            TelegramTestFactory.CreateMessageUpdate(text: "/myid", chatId: -1001234567890L, userId: 987654321L));

        await handler.HandleAsync(context, [], TestContext.Current.CancellationToken);

        Assert.Equal("-1001234567890|987654321", notifier.SentTexts[0].Text);
    }

    /// <summary>
    /// 回复模板可被应用层整体覆盖
    /// </summary>
    [Fact]
    public async Task HandleAsync_UsesConfiguredMyIdReply()
    {
        var handler = CreateHandler(out var notifier, out var options);
        options.CurrentValue.Texts.MyIdReply = "会话 {chatId} / 用户 {userId}";
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/myid", chatId: 100, userId: 200));

        await handler.HandleAsync(context, [], TestContext.Current.CancellationToken);

        Assert.Equal("会话 100 / 用户 200", notifier.SentTexts[0].Text);
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
    /// 回复模板被配置为空白时不发送
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenReplyTextBlank_SendsNothing()
    {
        var handler = CreateHandler(out var notifier, out var options);
        options.CurrentValue.Texts.MyIdReply = "   ";
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/myid"));

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
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/myid"));

        await handler.HandleAsync(context, [], TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 回复按 Reply 形式发给触发消息
    /// </summary>
    [Fact]
    public async Task HandleAsync_RepliesToTriggerMessage()
    {
        var handler = CreateHandler(out var notifier, out _);
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "main-bot"));
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/myid", messageId: 88));

        await handler.HandleAsync(context, [], TestContext.Current.CancellationToken);

        Assert.Equal("main-bot", notifier.SentTexts[0].BotName);
        Assert.Equal(88, notifier.SentTexts[0].ReplyToMessageId);
    }

    /// <summary>
    /// 构造内置 /myid 处理器
    /// </summary>
    /// <param name="notifier">发送门面替身</param>
    /// <param name="options">平台选项监视器</param>
    /// <returns>处理器</returns>
    private static MyIdCommandHandler CreateHandler(
        out FakeTelegramNotifier notifier,
        out TestOptionsMonitor<TelegramBotPlatformOptions> options)
    {
        notifier = new FakeTelegramNotifier();
        options = TelegramTestFactory.CreatePlatformOptions();
        return new MyIdCommandHandler(notifier, options, NullLogger<MyIdCommandHandler>.Instance);
    }
}
