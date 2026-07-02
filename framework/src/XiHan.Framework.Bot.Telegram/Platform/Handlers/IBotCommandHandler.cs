#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IBotCommandHandler
// Guid:e2104df7-6cf0-4ae1-a621-123ba863f016
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Telegram.Platform.Core;

namespace XiHan.Framework.Bot.Telegram.Platform.Handlers;

/// <summary>
/// Telegram 命令处理器（如 /start、/help；须配合 <see cref="BotCommandAttribute"/> 使用）
/// </summary>
public interface IBotCommandHandler
{
    /// <summary>
    /// 处理命令请求
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="args">命令参数（正则直达时为捕获组）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task HandleAsync(TelegramBotContext context, string[] args, CancellationToken cancellationToken = default);
}
