#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITelegramNotifier
// Guid:84e3f340-a66f-4138-811b-f65ae77b4ac3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace XiHan.Framework.Bot.Telegram.Messaging;

/// <summary>
/// Telegram 主动发送门面（按机器人名称定位实例；内建 429/5xx/超时重试退避与出站审计）
/// </summary>
public interface ITelegramNotifier
{
    /// <summary>
    /// 发送文本消息
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="chatId">目标会话 Id</param>
    /// <param name="text">文本内容</param>
    /// <param name="replyToMessageId">要回复的消息 Id（可选）</param>
    /// <param name="replyMarkup">键盘标记（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已发送消息</returns>
    Task<Message> SendTextAsync(string botName, long chatId, string text, int? replyToMessageId = null, ReplyMarkup? replyMarkup = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送 Markdown 消息
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="chatId">目标会话 Id</param>
    /// <param name="markdownText">Markdown 文本</param>
    /// <param name="replyToMessageId">要回复的消息 Id（可选）</param>
    /// <param name="replyMarkup">键盘标记（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已发送消息</returns>
    Task<Message> SendMarkdownAsync(string botName, long chatId, string markdownText, int? replyToMessageId = null, ReplyMarkup? replyMarkup = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按解析模式发送文本消息（None / Markdown / MarkdownV2 / Html，大小写不敏感）
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="chatId">目标会话 Id</param>
    /// <param name="text">文本内容</param>
    /// <param name="parseMode">解析模式（null 或 None 表示纯文本）</param>
    /// <param name="replyToMessageId">要回复的消息 Id（可选）</param>
    /// <param name="replyMarkup">键盘标记（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已发送消息</returns>
    Task<Message> SendByParseModeAsync(string botName, long chatId, string text, string? parseMode, int? replyToMessageId = null, ReplyMarkup? replyMarkup = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送图片消息
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="chatId">目标会话 Id</param>
    /// <param name="imageBytes">图片字节</param>
    /// <param name="caption">图片说明（可选）</param>
    /// <param name="replyToMessageId">要回复的消息 Id（可选）</param>
    /// <param name="replyMarkup">键盘标记（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已发送消息</returns>
    Task<Message> SendPhotoAsync(string botName, long chatId, byte[] imageBytes, string? caption = null, int? replyToMessageId = null, ReplyMarkup? replyMarkup = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送文件消息
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="chatId">目标会话 Id</param>
    /// <param name="fileBytes">文件字节</param>
    /// <param name="fileName">文件名</param>
    /// <param name="caption">文件说明（可选）</param>
    /// <param name="replyToMessageId">要回复的消息 Id（可选）</param>
    /// <param name="replyMarkup">键盘标记（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已发送消息</returns>
    Task<Message> SendDocumentAsync(string botName, long chatId, byte[] fileBytes, string fileName, string? caption = null, int? replyToMessageId = null, ReplyMarkup? replyMarkup = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 编辑已发送消息的文本
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="chatId">目标会话 Id</param>
    /// <param name="messageId">消息 Id</param>
    /// <param name="text">新文本内容</param>
    /// <param name="parseMode">解析模式（null 或 None 表示纯文本）</param>
    /// <param name="replyMarkup">内联键盘标记（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>编辑后的消息</returns>
    Task<Message> EditMessageTextAsync(string botName, long chatId, int messageId, string text, string? parseMode = null, InlineKeyboardMarkup? replyMarkup = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 编辑已发送消息的内联键盘（传 null 表示移除键盘）
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="chatId">目标会话 Id</param>
    /// <param name="messageId">消息 Id</param>
    /// <param name="replyMarkup">内联键盘标记（null 表示移除）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>编辑后的消息</returns>
    Task<Message> EditMessageReplyMarkupAsync(string botName, long chatId, int messageId, InlineKeyboardMarkup? replyMarkup = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 向机器人管理员逐个私发广播（单个失败仅告警，不中断）
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="text">文本内容</param>
    /// <param name="parseMode">解析模式（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SendToAdminsAsync(string botName, string text, string? parseMode = null, CancellationToken cancellationToken = default);
}
