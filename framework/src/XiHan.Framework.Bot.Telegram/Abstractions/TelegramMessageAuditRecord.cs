// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Bot.Telegram.Abstractions;

/// <summary>
/// Telegram 出站消息审计记录
/// </summary>
public sealed record TelegramMessageAuditRecord
{
    /// <summary>
    /// 机器人名称
    /// </summary>
    public string BotName { get; init; } = string.Empty;

    /// <summary>
    /// 机器人配置 Id（数据库来源可用，0 表示无）
    /// </summary>
    public long BotConfigId { get; init; }

    /// <summary>
    /// 目标会话 Id
    /// </summary>
    public long ChatId { get; init; }

    /// <summary>
    /// Bot API 方法名（如 sendMessage / sendPhoto / editMessageText）
    /// </summary>
    public string ApiMethod { get; init; } = string.Empty;

    /// <summary>
    /// 消息类型（text / photo / document 等）
    /// </summary>
    public string MessageType { get; init; } = string.Empty;

    /// <summary>
    /// 消息内容（文本或图片/文件说明）
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// 解析模式（None / Markdown / MarkdownV2 / Html）
    /// </summary>
    public string? ParseMode { get; init; }

    /// <summary>
    /// Telegram 返回的消息 Id（失败时为空）
    /// </summary>
    public int? TelegramMessageId { get; init; }

    /// <summary>
    /// 是否发送成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 错误码（Bot API 错误时有值）
    /// </summary>
    public int? ErrorCode { get; init; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 耗时毫秒
    /// </summary>
    public long ElapsedMs { get; init; }

    /// <summary>
    /// 发送时间
    /// </summary>
    public DateTimeOffset SendTime { get; init; } = DateTimeOffset.UtcNow;
}
