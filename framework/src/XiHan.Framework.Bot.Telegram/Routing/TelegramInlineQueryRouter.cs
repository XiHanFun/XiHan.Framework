// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Telegram.Bot;
using XiHan.Framework.Bot.Telegram.Core;
using XiHan.Framework.Bot.Telegram.Handlers;

namespace XiHan.Framework.Bot.Telegram.Routing;

/// <summary>
/// 内联查询路由器：把 @bot query 分发到 <see cref="IBotInlineQueryHandler"/>
/// </summary>
public sealed class TelegramInlineQueryRouter
{
    private const int AnswerCacheSeconds = 300;

    private readonly TelegramBotHandlerCatalog _catalog;
    private readonly ILogger<TelegramInlineQueryRouter> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="catalog">处理器目录</param>
    /// <param name="logger">日志记录器</param>
    public TelegramInlineQueryRouter(TelegramBotHandlerCatalog catalog, ILogger<TelegramInlineQueryRouter> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    /// <summary>
    /// 分发内联查询给首个满足条件的处理器
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="scopedProvider">作用域服务提供者（用于实例化处理器）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否已处理</returns>
    public async Task<bool> HandleAsync(
        TelegramBotContext context,
        IServiceProvider scopedProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(scopedProvider);

        var inlineQuery = context.Update.InlineQuery;
        if (inlineQuery is null)
        {
            return false;
        }

        var query = inlineQuery.Query?.Trim() ?? string.Empty;

        foreach (var handlerType in _catalog.InlineQueryHandlerTypes)
        {
            if (scopedProvider.GetService(handlerType) is not IBotInlineQueryHandler handler)
            {
                _logger.LogError(
                    "Telegram 内联查询处理器未在 DI 注册：{HandlerType}（请使用 AddTelegramBotHandler<THandler>() 注册）。",
                    handlerType.FullName);
                continue;
            }

            if (!handler.CanHandle(context, query))
            {
                continue;
            }

            try
            {
                var results = await handler.HandleAsync(context, query, cancellationToken);
                await context.Client.AnswerInlineQuery(
                    inlineQueryId: inlineQuery.Id,
                    results: results,
                    cacheTime: AnswerCacheSeconds,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Telegram 内联查询处理失败。Bot={BotName}, Query={Query}", context.Bot.Name, query);
            }

            return true;
        }

        return false;
    }
}
