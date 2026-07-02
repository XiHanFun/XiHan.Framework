#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ConversationState
// Guid:75087689-8c29-43de-bf25-3d7683e64f98
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
