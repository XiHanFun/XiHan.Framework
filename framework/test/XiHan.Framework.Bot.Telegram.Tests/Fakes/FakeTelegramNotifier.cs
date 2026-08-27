// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using XiHan.Framework.Bot.Telegram.Messaging;

namespace XiHan.Framework.Bot.Telegram.Tests.Fakes;

/// <summary>
/// 发送门面手写替身
/// </summary>
/// <remarks>
/// 路由器、分发器与三个内置命令处理器都只依赖 <see cref="ITelegramNotifier"/> 发消息，
/// 换成本替身即可在完全离线的前提下断言「发给谁、发了什么、有没有按 Reply 发」。
/// </remarks>
internal sealed class FakeTelegramNotifier : ITelegramNotifier
{
    private int _messageId;

    /// <summary>
    /// 已记录的文本发送调用
    /// </summary>
    public List<SentText> SentTexts { get; } = [];

    /// <summary>
    /// 向管理员广播的文本
    /// </summary>
    public List<string> AdminBroadcasts { get; } = [];

    /// <summary>
    /// 设置后所有发送均抛出该异常（模拟发送门面重试耗尽后的最终失败）
    /// </summary>
    public Exception? ExceptionToThrow { get; set; }

    /// <summary>
    /// 最后一次收到的取消令牌
    /// </summary>
    public CancellationToken LastCancellationToken { get; private set; }

    /// <summary>
    /// 发送文本消息
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="chatId">目标会话 Id</param>
    /// <param name="text">文本内容</param>
    /// <param name="replyToMessageId">要回复的消息 Id</param>
    /// <param name="replyMarkup">键盘标记</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已发送消息</returns>
    public Task<Message> SendTextAsync(string botName, long chatId, string text, int? replyToMessageId = null, ReplyMarkup? replyMarkup = null, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        SentTexts.Add(new SentText(botName, chatId, text, replyToMessageId, replyMarkup));
        return CompleteAsync(chatId, text);
    }

    /// <summary>
    /// 发送 Markdown 消息
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="chatId">目标会话 Id</param>
    /// <param name="markdownText">Markdown 文本</param>
    /// <param name="replyToMessageId">要回复的消息 Id</param>
    /// <param name="replyMarkup">键盘标记</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已发送消息</returns>
    public Task<Message> SendMarkdownAsync(string botName, long chatId, string markdownText, int? replyToMessageId = null, ReplyMarkup? replyMarkup = null, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        SentTexts.Add(new SentText(botName, chatId, markdownText, replyToMessageId, replyMarkup));
        return CompleteAsync(chatId, markdownText);
    }

    /// <summary>
    /// 按解析模式发送文本消息
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="chatId">目标会话 Id</param>
    /// <param name="text">文本内容</param>
    /// <param name="parseMode">解析模式</param>
    /// <param name="replyToMessageId">要回复的消息 Id</param>
    /// <param name="replyMarkup">键盘标记</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已发送消息</returns>
    public Task<Message> SendByParseModeAsync(string botName, long chatId, string text, string? parseMode, int? replyToMessageId = null, ReplyMarkup? replyMarkup = null, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        SentTexts.Add(new SentText(botName, chatId, text, replyToMessageId, replyMarkup));
        return CompleteAsync(chatId, text);
    }

    /// <summary>
    /// 发送图片消息
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="chatId">目标会话 Id</param>
    /// <param name="imageBytes">图片字节</param>
    /// <param name="caption">图片说明</param>
    /// <param name="replyToMessageId">要回复的消息 Id</param>
    /// <param name="replyMarkup">键盘标记</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已发送消息</returns>
    public Task<Message> SendPhotoAsync(string botName, long chatId, byte[] imageBytes, string? caption = null, int? replyToMessageId = null, ReplyMarkup? replyMarkup = null, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        SentTexts.Add(new SentText(botName, chatId, caption ?? string.Empty, replyToMessageId, replyMarkup));
        return CompleteAsync(chatId, caption ?? string.Empty);
    }

    /// <summary>
    /// 发送文件消息
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="chatId">目标会话 Id</param>
    /// <param name="fileBytes">文件字节</param>
    /// <param name="fileName">文件名</param>
    /// <param name="caption">文件说明</param>
    /// <param name="replyToMessageId">要回复的消息 Id</param>
    /// <param name="replyMarkup">键盘标记</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已发送消息</returns>
    public Task<Message> SendDocumentAsync(string botName, long chatId, byte[] fileBytes, string fileName, string? caption = null, int? replyToMessageId = null, ReplyMarkup? replyMarkup = null, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        SentTexts.Add(new SentText(botName, chatId, caption ?? fileName, replyToMessageId, replyMarkup));
        return CompleteAsync(chatId, caption ?? fileName);
    }

    /// <summary>
    /// 编辑已发送消息的文本
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="chatId">目标会话 Id</param>
    /// <param name="messageId">消息 Id</param>
    /// <param name="text">新文本内容</param>
    /// <param name="parseMode">解析模式</param>
    /// <param name="replyMarkup">内联键盘标记</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>编辑后的消息</returns>
    public Task<Message> EditMessageTextAsync(string botName, long chatId, int messageId, string text, string? parseMode = null, InlineKeyboardMarkup? replyMarkup = null, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        SentTexts.Add(new SentText(botName, chatId, text, messageId, replyMarkup));
        return CompleteAsync(chatId, text);
    }

    /// <summary>
    /// 编辑已发送消息的内联键盘
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="chatId">目标会话 Id</param>
    /// <param name="messageId">消息 Id</param>
    /// <param name="replyMarkup">内联键盘标记</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>编辑后的消息</returns>
    public Task<Message> EditMessageReplyMarkupAsync(string botName, long chatId, int messageId, InlineKeyboardMarkup? replyMarkup = null, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        SentTexts.Add(new SentText(botName, chatId, string.Empty, messageId, replyMarkup));
        return CompleteAsync(chatId, string.Empty);
    }

    /// <summary>
    /// 向机器人管理员广播
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="text">文本内容</param>
    /// <param name="parseMode">解析模式</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task SendToAdminsAsync(string botName, string text, string? parseMode = null, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        AdminBroadcasts.Add(text);
        return ExceptionToThrow is null ? Task.CompletedTask : Task.FromException(ExceptionToThrow);
    }

    private Task<Message> CompleteAsync(long chatId, string text)
    {
        if (ExceptionToThrow is not null)
        {
            return Task.FromException<Message>(ExceptionToThrow);
        }

        _messageId++;
        return Task.FromResult(new Message
        {
            Id = _messageId,
            Chat = new Chat { Id = chatId, Type = ChatType.Private },
            Text = text
        });
    }
}

/// <summary>
/// 一次文本发送调用的快照
/// </summary>
/// <param name="BotName">机器人名称</param>
/// <param name="ChatId">目标会话 Id</param>
/// <param name="Text">文本内容</param>
/// <param name="ReplyToMessageId">要回复的消息 Id</param>
/// <param name="ReplyMarkup">键盘标记</param>
internal sealed record SentText(string BotName, long ChatId, string Text, int? ReplyToMessageId, ReplyMarkup? ReplyMarkup);
