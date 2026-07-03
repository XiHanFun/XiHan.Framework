#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IBotStartPayloadHandler
// Guid:bd0bcba8-d622-4713-b20b-66dd5f7ca745
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Telegram.Core;

namespace XiHan.Framework.Bot.Telegram.Handlers;

/// <summary>
/// Telegram /start 深链参数处理器（t.me/{bot}?start=payload）
/// </summary>
/// <remarks>
/// 分发器在命令路由前触发该链：/start 携带参数时按 Order 依次执行，
/// 任一处理器返回 true 即消费本次 /start，不再进入命令路由。
/// </remarks>
public interface IBotStartPayloadHandler
{
    /// <summary>
    /// 执行顺序（值越小优先级越高）
    /// </summary>
    int Order => 0;

    /// <summary>
    /// 处理 /start 深链参数
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="payload">深链参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>true 表示已消费本次 /start</returns>
    Task<bool> HandleAsync(TelegramBotContext context, string payload, CancellationToken cancellationToken = default);
}
