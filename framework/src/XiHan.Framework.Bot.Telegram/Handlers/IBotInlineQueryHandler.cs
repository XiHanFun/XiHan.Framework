#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IBotInlineQueryHandler
// Guid:77fe03d8-2aed-4fd5-9ca5-8a9034bf5073
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Telegram.Bot.Types.InlineQueryResults;
using XiHan.Framework.Bot.Telegram.Core;

namespace XiHan.Framework.Bot.Telegram.Handlers;

/// <summary>
/// Telegram 内联查询处理器（@bot query）
/// </summary>
public interface IBotInlineQueryHandler
{
    /// <summary>
    /// 判断当前内联查询是否由该处理器处理
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="query">查询文本</param>
    /// <returns>是否处理</returns>
    bool CanHandle(TelegramBotContext context, string query);

    /// <summary>
    /// 处理内联查询，返回结果列表
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="query">查询文本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>内联查询结果列表</returns>
    Task<IReadOnlyList<InlineQueryResult>> HandleAsync(TelegramBotContext context, string query, CancellationToken cancellationToken = default);
}
