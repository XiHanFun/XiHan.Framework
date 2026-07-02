#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IBotStateHandler
// Guid:3825eb5e-9fc1-41ba-af43-3e23e83f7cdd
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Telegram.Platform.Abstractions;
using XiHan.Framework.Bot.Telegram.Platform.Core;

namespace XiHan.Framework.Bot.Telegram.Platform.Handlers;

/// <summary>
/// 会话状态处理器：当会话存在活跃 <see cref="ConversationState"/> 时，
/// 非命令、非回调的消息会优先路由到匹配的处理器
/// </summary>
public interface IBotStateHandler
{
    /// <summary>
    /// 执行顺序（值越小优先级越高）
    /// </summary>
    int Order => 0;

    /// <summary>
    /// 判断当前状态步骤是否由该处理器处理
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="stateStep">当前步骤标识</param>
    /// <returns>是否处理</returns>
    bool CanHandle(TelegramBotContext context, string stateStep);

    /// <summary>
    /// 处理当前步骤的消息
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="stateStep">当前步骤标识</param>
    /// <param name="statePayload">状态上下文数据 JSON</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task HandleAsync(TelegramBotContext context, string stateStep, string? statePayload, CancellationToken cancellationToken = default);
}
