// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Core;

namespace XiHan.Framework.Bot.Telegram.Handlers;

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
