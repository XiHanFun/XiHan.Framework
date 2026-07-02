#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IBotMessageHandler
// Guid:d2c9aa47-5757-4c41-ba99-a7e6be49974a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Telegram.Platform.Core;

namespace XiHan.Framework.Bot.Telegram.Platform.Handlers;

/// <summary>
/// 普通消息处理器（非命令、非回调；按 Order 排序，首个命中即停）
/// </summary>
public interface IBotMessageHandler
{
    /// <summary>
    /// 执行顺序（值越小优先级越高）
    /// </summary>
    int Order => 0;

    /// <summary>
    /// 判断当前消息是否由该处理器处理
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <returns>是否处理</returns>
    bool CanHandle(TelegramBotContext context);

    /// <summary>
    /// 处理消息
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task HandleAsync(TelegramBotContext context, CancellationToken cancellationToken = default);
}
