// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Bot.Telegram.Abstractions;

/// <summary>
/// 会话多步交互状态（每个 机器人+会话+用户 同时只存在一个活跃状态）
/// </summary>
public class ConversationState
{
    /// <summary>
    /// 当前步骤标识（由业务处理器定义，如 awaiting_amount）
    /// </summary>
    public string Step { get; set; } = string.Empty;

    /// <summary>
    /// 上下文数据 JSON（由业务处理器写入，下一步处理器解析）
    /// </summary>
    public string? Payload { get; set; }

    /// <summary>
    /// 状态创建时间
    /// </summary>
    public DateTimeOffset CreateTime { get; set; } = DateTimeOffset.UtcNow;
}
