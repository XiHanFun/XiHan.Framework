// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Handlers;
using XiHan.Framework.Bot.Telegram.Messaging;
using XiHan.Framework.Bot.Telegram.MultiBot;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Routing;

namespace XiHan.Framework.Bot.Telegram.Core;

/// <summary>
/// Telegram Update 分发入口（固定次序管线）
/// </summary>
/// <remarks>
/// 次序：群组/频道白名单守卫（/start /myid /help 永久放行）→ update_id 幂等 → 内联查询 →
/// 会话状态机 → 回调路由 → /start 深链 → 命令路由 → 回复路由 → 消息路由 → 兜底回复；
/// 处理被取消时回滚幂等标记（at-least-once），任意异常记日志并向用户发送兜底错误文案。
/// </remarks>
public sealed class TelegramUpdateDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TelegramBotHandlerCatalog _catalog;
    private readonly TelegramCommandRouter _commandRouter;
    private readonly TelegramCallbackRouter _callbackRouter;
    private readonly TelegramReplyRouter _replyRouter;
    private readonly TelegramMessageRouter _messageRouter;
    private readonly TelegramInlineQueryRouter _inlineQueryRouter;
    private readonly ITelegramUpdateDeduplicator _deduplicator;
    private readonly IConversationStateStore _conversationStateStore;
    private readonly ITelegramNotifier _notifier;
    private readonly IOptionsMonitor<TelegramBotPlatformOptions> _options;
    private readonly ILogger<TelegramUpdateDispatcher> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="scopeFactory">服务作用域工厂</param>
    /// <param name="catalog">处理器目录</param>
    /// <param name="commandRouter">命令路由器</param>
    /// <param name="callbackRouter">回调路由器</param>
    /// <param name="replyRouter">回复路由器</param>
    /// <param name="messageRouter">消息路由器</param>
    /// <param name="inlineQueryRouter">内联查询路由器</param>
    /// <param name="deduplicator">Update 幂等去重器</param>
    /// <param name="conversationStateStore">会话状态存储</param>
    /// <param name="notifier">发送门面</param>
    /// <param name="options">平台选项监视器</param>
    /// <param name="logger">日志记录器</param>
    public TelegramUpdateDispatcher(
        IServiceScopeFactory scopeFactory,
        TelegramBotHandlerCatalog catalog,
        TelegramCommandRouter commandRouter,
        TelegramCallbackRouter callbackRouter,
        TelegramReplyRouter replyRouter,
        TelegramMessageRouter messageRouter,
        TelegramInlineQueryRouter inlineQueryRouter,
        ITelegramUpdateDeduplicator deduplicator,
        IConversationStateStore conversationStateStore,
        ITelegramNotifier notifier,
        IOptionsMonitor<TelegramBotPlatformOptions> options,
        ILogger<TelegramUpdateDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _catalog = catalog;
        _commandRouter = commandRouter;
        _callbackRouter = callbackRouter;
        _replyRouter = replyRouter;
        _messageRouter = messageRouter;
        _inlineQueryRouter = inlineQueryRouter;
        _deduplicator = deduplicator;
        _conversationStateStore = conversationStateStore;
        _notifier = notifier;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 分发单条 Update
    /// </summary>
    /// <param name="bot">机器人实例</param>
    /// <param name="update">Telegram Update</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task DispatchAsync(BotInstance bot, Update update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bot);
        ArgumentNullException.ThrowIfNull(update);

        var context = new TelegramBotContext(bot, update);
        var marked = false;

        try
        {
            // ── 群组/频道白名单守卫（fail-closed：白名单为空拒收所有群组与频道；永久放行命令例外） ──
            var bypassGroupGuard = TelegramCommandGuards.IsAlwaysAvailableCommandToken(context.GetCommandToken());
            if ((context.IsGroup || context.IsChannel) && !bot.IsGroupAllowed(context.ChatId) && !bypassGroupGuard)
            {
                _logger.LogWarning(
                    "Telegram Update 已忽略（群组/频道未授权）。Bot={BotName}, ChatId={ChatId}, UpdateId={UpdateId}, Type={UpdateType}",
                    bot.Name, context.ChatId, update.Id, update.Type);
                return;
            }

            // ── update_id 幂等：拦截 Webhook 重发 / 轮询重复投递 ──
            if (!await _deduplicator.TryMarkProcessedAsync(bot.Name, update.Id, cancellationToken))
            {
                _logger.LogInformation(
                    "Telegram Update 跳过（命中幂等）。Bot={BotName}, UpdateId={UpdateId}, Type={UpdateType}",
                    bot.Name, update.Id, update.Type);
                return;
            }

            marked = true;

            // ── 内联查询：@bot query，无 chat 上下文，直接路由 ──
            if (update.InlineQuery is not null)
            {
                using var inlineScope = _scopeFactory.CreateScope();
                _ = await _inlineQueryRouter.HandleAsync(context, inlineScope.ServiceProvider, cancellationToken);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var provider = scope.ServiceProvider;

            // ── 实时解析兜底回复开关（全局设置与单机器人配置任一开启即生效） ──
            await ApplyFallbackFlagAsync(context, provider, cancellationToken);

            // ── 会话状态机：非命令、非回调消息优先检查活跃会话状态 ──
            if (!context.IsCallback
                && !context.IsCommand
                && context.Message is not null
                && await TryRouteConversationStateAsync(context, provider, cancellationToken))
            {
                return;
            }

            // ── 回调路由 ──
            if (context.IsCallback)
            {
                _ = await _callbackRouter.HandleAsync(context, provider, cancellationToken);
                return;
            }

            // ── /start 深链参数链（任一处理器消费即结束） ──
            if (await TryRouteStartPayloadAsync(context, provider, cancellationToken))
            {
                return;
            }

            // ── 命令路由 ──
            if (await _commandRouter.HandleAsync(context, provider, cancellationToken))
            {
                return;
            }

            // ── 回复路由 ──
            if (context.IsReply && await _replyRouter.HandleAsync(context, provider, cancellationToken))
            {
                return;
            }

            // ── 消息路由 ──
            if (context.Message is not null && await _messageRouter.HandleAsync(context, provider, cancellationToken))
            {
                return;
            }

            // ── 兜底回复 ──
            await TrySendFallbackReplyAsync(context, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // at-least-once：处理半途被取消时回滚幂等标记，让重发/重投后仍有机会重新处理；
            // 回滚必须传 CancellationToken.None，原令牌已取消会导致回滚本身被取消
            if (marked)
            {
                await _deduplicator.TryUnmarkAsync(bot.Name, update.Id, CancellationToken.None);
                _logger.LogWarning(
                    "Telegram Update 处理被取消，已回滚幂等标记。Bot={BotName}, UpdateId={UpdateId}, Type={UpdateType}",
                    bot.Name, update.Id, update.Type);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Telegram Update 分发失败。Bot={BotName}, UpdateId={UpdateId}, Type={UpdateType}",
                bot.Name, update.Id, update.Type);
            await TrySendInternalErrorReplyAsync(context, cancellationToken);
        }
    }

    private async Task ApplyFallbackFlagAsync(TelegramBotContext context, IServiceProvider provider, CancellationToken cancellationToken)
    {
        var enabled = context.Bot.Config.EnableFallbackReply;
        try
        {
            var settingsStore = provider.GetService<ITelegramBotSettingsStore>();
            if (settingsStore is not null)
            {
                var settings = await settingsStore.GetSettingsAsync(cancellationToken);
                enabled = enabled || settings.EnableFallbackReply;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram 兜底回复开关解析失败，按机器人配置处理。Bot={BotName}", context.Bot.Name);
        }

        context.EnableFallbackReply = enabled;
    }

    private async Task<bool> TryRouteConversationStateAsync(
        TelegramBotContext context,
        IServiceProvider provider,
        CancellationToken cancellationToken)
    {
        if (_catalog.StateHandlerTypes.Count == 0 || context.ChatId == 0)
        {
            return false;
        }

        var state = await _conversationStateStore.GetAsync(context.Bot.Name, context.ChatId, context.UserId, cancellationToken);
        if (state is null || string.IsNullOrWhiteSpace(state.Step))
        {
            return false;
        }

        var handlers = TelegramHandlerResolver.ResolveOrdered<IBotStateHandler>(
            provider, _catalog.StateHandlerTypes, x => x.Order, _logger);

        foreach (var handler in handlers)
        {
            if (!handler.CanHandle(context, state.Step))
            {
                continue;
            }

            await handler.HandleAsync(context, state.Step, state.Payload, cancellationToken);
            return true;
        }

        // 无匹配状态处理器时不清除状态（可能下一条消息会匹配）
        return false;
    }

    private async Task<bool> TryRouteStartPayloadAsync(
        TelegramBotContext context,
        IServiceProvider provider,
        CancellationToken cancellationToken)
    {
        if (_catalog.StartPayloadHandlerTypes.Count == 0 || !context.IsCommand)
        {
            return false;
        }

        var command = TelegramCommandGuards.NormalizeCommandToken(context.GetCommandToken());
        if (!string.Equals(command, TelegramBotPlatformConsts.StartCommand, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = context.GetCommandArgs().FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        var handlers = TelegramHandlerResolver.ResolveOrdered<IBotStartPayloadHandler>(
            provider, _catalog.StartPayloadHandlerTypes, x => x.Order, _logger);

        foreach (var handler in handlers)
        {
            try
            {
                if (await handler.HandleAsync(context, payload, cancellationToken))
                {
                    return true;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Telegram /start 深链参数处理失败。Bot={BotName}, Handler={HandlerType}, Payload={Payload}",
                    context.Bot.Name, handler.GetType().FullName, payload);
            }
        }

        return false;
    }

    private async Task TrySendFallbackReplyAsync(TelegramBotContext context, CancellationToken cancellationToken)
    {
        if (!context.EnableFallbackReply || context.Message is null || context.ChatId == 0)
        {
            return;
        }

        var text = _options.CurrentValue.Texts.UnhandledMessageReply;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            _ = await _notifier.SendTextAsync(context.Bot.Name, context.ChatId, text, context.TriggerMessageId, replyMarkup: null, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram 兜底回复发送失败。Bot={BotName}, ChatId={ChatId}", context.Bot.Name, context.ChatId);
        }
    }

    private async Task TrySendInternalErrorReplyAsync(TelegramBotContext context, CancellationToken cancellationToken)
    {
        if (context.ChatId == 0)
        {
            _logger.LogWarning(
                "Telegram Update 异常兜底响应跳过（无法定位会话）。Bot={BotName}, UpdateId={UpdateId}",
                context.Bot.Name, context.Update.Id);
            return;
        }

        var text = _options.CurrentValue.Texts.InternalErrorReply;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            _ = await _notifier.SendTextAsync(context.Bot.Name, context.ChatId, text, context.TriggerMessageId, replyMarkup: null, cancellationToken);
        }
        catch (Exception ex) when (context.TriggerMessageId.HasValue)
        {
            _logger.LogWarning(ex,
                "Telegram 异常兜底响应按 Reply 发送失败，改为普通消息。Bot={BotName}, ChatId={ChatId}",
                context.Bot.Name, context.ChatId);
            await TrySendInternalErrorPlainReplyAsync(context, text, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Telegram 异常兜底响应发送失败。Bot={BotName}, ChatId={ChatId}",
                context.Bot.Name, context.ChatId);
        }
    }

    private async Task TrySendInternalErrorPlainReplyAsync(TelegramBotContext context, string text, CancellationToken cancellationToken)
    {
        try
        {
            _ = await _notifier.SendTextAsync(context.Bot.Name, context.ChatId, text, replyToMessageId: null, replyMarkup: null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Telegram 异常兜底普通消息发送失败。Bot={BotName}, ChatId={ChatId}",
                context.Bot.Name, context.ChatId);
        }
    }
}
