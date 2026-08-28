// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Telegram.Options;

namespace XiHan.Framework.Bot.Telegram.Tests.Options;

/// <summary>
/// <see cref="TelegramBotTexts"/> 平台文案配置测试
/// </summary>
/// <remarks>
/// 文案带中文默认值是刻意设计：不配置也能给用户可读回复。
/// 这里锁住「默认值非空」与两条模板文案的占位符名，占位符改名会让内置命令回复出现原样的 {chatId}。
/// </remarks>
public class TelegramBotTextsTests
{
    /// <summary>
    /// 全部文案默认非空，避免未配置时回复空白
    /// </summary>
    [Fact]
    public void Defaults_AllTextsAreNotBlank()
    {
        var texts = new TelegramBotTexts();

        Assert.False(string.IsNullOrWhiteSpace(texts.InternalErrorReply));
        Assert.False(string.IsNullOrWhiteSpace(texts.CommandDisabledReply));
        Assert.False(string.IsNullOrWhiteSpace(texts.AdminOnlyCommandReply));
        Assert.False(string.IsNullOrWhiteSpace(texts.AdminOnlyCallbackReply));
        Assert.False(string.IsNullOrWhiteSpace(texts.UnhandledMessageReply));
        Assert.False(string.IsNullOrWhiteSpace(texts.SendFailureAdminNotifyTitle));
        Assert.False(string.IsNullOrWhiteSpace(texts.StartReply));
        Assert.False(string.IsNullOrWhiteSpace(texts.HelpHeader));
        Assert.False(string.IsNullOrWhiteSpace(texts.MyIdReply));
    }

    /// <summary>
    /// /start 欢迎文案带 botUsername 占位符
    /// </summary>
    [Fact]
    public void Defaults_StartReplyContainsBotUsernamePlaceholder()
    {
        Assert.Contains("{botUsername}", new TelegramBotTexts().StartReply, StringComparison.Ordinal);
    }

    /// <summary>
    /// /myid 回复模板同时带 chatId 与 userId 占位符
    /// </summary>
    [Fact]
    public void Defaults_MyIdReplyContainsBothPlaceholders()
    {
        var texts = new TelegramBotTexts();

        Assert.Contains("{chatId}", texts.MyIdReply, StringComparison.Ordinal);
        Assert.Contains("{userId}", texts.MyIdReply, StringComparison.Ordinal);
    }

    /// <summary>
    /// 兜底回复文案引导用户去看 /help
    /// </summary>
    [Fact]
    public void Defaults_UnhandledMessageReplyPointsToHelpCommand()
    {
        Assert.Contains("/help", new TelegramBotTexts().UnhandledMessageReply, StringComparison.Ordinal);
    }

    /// <summary>
    /// 文案可被应用层整体覆盖
    /// </summary>
    [Fact]
    public void Properties_AreMutable()
    {
        var texts = new TelegramBotTexts
        {
            InternalErrorReply = "internal",
            CommandDisabledReply = "disabled",
            AdminOnlyCommandReply = "admin-command",
            AdminOnlyCallbackReply = "admin-callback",
            UnhandledMessageReply = "unhandled",
            SendFailureAdminNotifyTitle = "failure",
            StartReply = "start",
            HelpHeader = "help",
            MyIdReply = "myid"
        };

        Assert.Equal("internal", texts.InternalErrorReply);
        Assert.Equal("disabled", texts.CommandDisabledReply);
        Assert.Equal("admin-command", texts.AdminOnlyCommandReply);
        Assert.Equal("admin-callback", texts.AdminOnlyCallbackReply);
        Assert.Equal("unhandled", texts.UnhandledMessageReply);
        Assert.Equal("failure", texts.SendFailureAdminNotifyTitle);
        Assert.Equal("start", texts.StartReply);
        Assert.Equal("help", texts.HelpHeader);
        Assert.Equal("myid", texts.MyIdReply);
    }
}
