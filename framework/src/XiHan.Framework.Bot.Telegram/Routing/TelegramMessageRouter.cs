// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Bot.Telegram.Core;
using XiHan.Framework.Bot.Telegram.Handlers;

namespace XiHan.Framework.Bot.Telegram.Routing;

/// <summary>
/// 普通消息路由器（非命令、非回调、非回复；按 Order 排序，首个命中即停）
/// </summary>
public sealed class TelegramMessageRouter
{
    private readonly TelegramBotHandlerCatalog _catalog;
    private readonly ILogger<TelegramMessageRouter> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="catalog">处理器目录</param>
    /// <param name="logger">日志记录器</param>
    public TelegramMessageRouter(TelegramBotHandlerCatalog catalog, ILogger<TelegramMessageRouter> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    /// <summary>
    /// 分发消息给首个满足条件的消息处理器
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

        var handlers = TelegramHandlerResolver.ResolveOrdered<IBotMessageHandler>(
            scopedProvider, _catalog.MessageHandlerTypes, x => x.Order, _logger);

        foreach (var handler in handlers)
        {
            if (!handler.CanHandle(context))
            {
                continue;
            }

            await handler.HandleAsync(context, cancellationToken);
            return true;
        }

        return false;
    }
}
