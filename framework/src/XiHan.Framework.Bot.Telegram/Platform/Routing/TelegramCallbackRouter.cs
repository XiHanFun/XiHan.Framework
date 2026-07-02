#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TelegramCallbackRouter
// Guid:6ee35345-568c-4b53-9541-91d373ffb5c1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using XiHan.Framework.Bot.Telegram.Platform.Core;
using XiHan.Framework.Bot.Telegram.Platform.Handlers;
using XiHan.Framework.Bot.Telegram.Platform.Messaging;
using XiHan.Framework.Bot.Telegram.Platform.Options;

namespace XiHan.Framework.Bot.Telegram.Platform.Routing;

/// <summary>
/// 按钮回调路由器：把 callback data 分发到标注了 <see cref="BotCallbackAttribute"/> 的处理器
/// </summary>
/// <remarks>
/// finally 中必答 AnswerCallbackQuery（避免客户端持续 loading）；
/// 处理器可通过 <see cref="TelegramBotContext.SetCallbackAnswer"/> 自定义应答文本/弹窗，
/// 或调用 <see cref="TelegramBotContext.MarkCallbackAnswered"/> 声明已自行应答。
/// </remarks>
public sealed class TelegramCallbackRouter
{
    private readonly TelegramBotHandlerCatalog _catalog;
    private readonly ITelegramNotifier _notifier;
    private readonly IOptionsMonitor<TelegramBotPlatformOptions> _options;
    private readonly ILogger<TelegramCallbackRouter> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="catalog">处理器目录</param>
    /// <param name="notifier">发送门面</param>
    /// <param name="options">平台选项监视器</param>
    /// <param name="logger">日志记录器</param>
    public TelegramCallbackRouter(
        TelegramBotHandlerCatalog catalog,
        ITelegramNotifier notifier,
        IOptionsMonitor<TelegramBotPlatformOptions> options,
        ILogger<TelegramCallbackRouter> logger)
    {
        _catalog = catalog;
        _notifier = notifier;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 处理按钮回调
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

        var data = context.Callback?.Data;
        var action = context.GetCallbackAction();

        if (string.IsNullOrWhiteSpace(data) || string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        if (!_catalog.CallbackRoutes.TryGetValue(action, out var route))
        {
            return false;
        }

        try
        {
            if (route.AdminOnly && !context.IsAdmin)
            {
                await TryReplyAsync(context, _options.CurrentValue.Texts.AdminOnlyCallbackReply, cancellationToken);
                return true;
            }

            if (scopedProvider.GetService(route.HandlerType) is not IBotCallbackHandler handler)
            {
                _logger.LogError(
                    "Telegram 回调处理器未在 DI 注册：{HandlerType}（请使用 AddTelegramBotHandler<THandler>() 注册）。Bot={BotName}",
                    route.HandlerType.FullName, context.Bot.Name);
                return false;
            }

            await handler.HandleAsync(context, data, cancellationToken);
        }
        finally
        {
            await TryAnswerCallbackAsync(context, action, cancellationToken);
        }

        return true;
    }

    private async Task TryAnswerCallbackAsync(TelegramBotContext context, string action, CancellationToken cancellationToken)
    {
        if (context.CallbackAnswered || string.IsNullOrWhiteSpace(context.Callback?.Id))
        {
            return;
        }

        try
        {
            await context.Client.AnswerCallbackQuery(
                callbackQueryId: context.Callback.Id,
                text: string.IsNullOrWhiteSpace(context.CallbackAnswerText) ? null : context.CallbackAnswerText,
                showAlert: context.CallbackAnswerShowAlert,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 请求已取消，吞下异常以免覆盖处理器可能抛出的原始异常
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram 回调应答失败。Bot={BotName}, Action={Action}", context.Bot.Name, action);
        }
    }

    private async Task TryReplyAsync(TelegramBotContext context, string text, CancellationToken cancellationToken)
    {
        if (context.ChatId == 0 || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            _ = await _notifier.SendTextAsync(context.Bot.Name, context.ChatId, text, context.TriggerMessageId, replyMarkup: null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram 回调守卫回复发送失败。Bot={BotName}, ChatId={ChatId}", context.Bot.Name, context.ChatId);
        }
    }
}
