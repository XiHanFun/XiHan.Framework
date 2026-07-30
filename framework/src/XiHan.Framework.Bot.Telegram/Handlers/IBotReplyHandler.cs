// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Telegram.Core;

namespace XiHan.Framework.Bot.Telegram.Handlers;

/// <summary>
/// 回复消息处理器（用户 Reply 某条消息；按 Order 排序，首个命中即停）
/// </summary>
public interface IBotReplyHandler
{
    /// <summary>
    /// 执行顺序（值越小优先级越高）
    /// </summary>
    int Order => 0;

    /// <summary>
    /// 判断当前回复是否由该处理器处理
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <returns>是否处理</returns>
    bool CanHandle(TelegramBotContext context);

    /// <summary>
    /// 处理回复消息
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task HandleAsync(TelegramBotContext context, CancellationToken cancellationToken = default);
}
