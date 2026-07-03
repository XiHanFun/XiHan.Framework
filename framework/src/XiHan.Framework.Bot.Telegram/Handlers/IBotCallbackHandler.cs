#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IBotCallbackHandler
// Guid:570185ba-40c1-4902-8db8-f120498885f9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Telegram.Core;

namespace XiHan.Framework.Bot.Telegram.Handlers;

/// <summary>
/// Telegram 按钮回调处理器（InlineKeyboard CallbackQuery；须配合 <see cref="BotCallbackAttribute"/> 使用）
/// </summary>
public interface IBotCallbackHandler
{
    /// <summary>
    /// 处理回调数据（如 confirm:12345）
    /// </summary>
    /// <param name="context">更新上下文（可通过 SetCallbackAnswer 自定义应答文本/弹窗）</param>
    /// <param name="data">完整回调数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task HandleAsync(TelegramBotContext context, string data, CancellationToken cancellationToken = default);
}
