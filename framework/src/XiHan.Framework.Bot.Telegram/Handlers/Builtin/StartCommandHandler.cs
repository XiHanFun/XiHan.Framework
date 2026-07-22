// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Telegram.Core;
using XiHan.Framework.Bot.Telegram.Messaging;
using XiHan.Framework.Bot.Telegram.Options;

namespace XiHan.Framework.Bot.Telegram.Handlers.Builtin;

/// <summary>
/// 内置 /start 命令处理器：发送欢迎文案（<see cref="TelegramBotTexts.StartReply"/>，支持 {botUsername} 占位符）
/// </summary>
/// <remarks>
/// 深链参数（t.me/{bot}?start=payload）由分发器在命令路由前交给 <see cref="IBotStartPayloadHandler"/> 链前置消费，
/// 到达本处理器的通常是无参数的普通 /start；未被消费的深链参数在此忽略，统一回复欢迎文案。
/// 须显式注册（AddTelegramBotBuiltinHandlers 或 AddTelegramBotHandler&lt;StartCommandHandler&gt;()）。
/// </remarks>
[BotCommand("/start", Description = "开始使用机器人")]
public sealed class StartCommandHandler : IBotCommandHandler
{
    private readonly ITelegramNotifier _notifier;
    private readonly IOptionsMonitor<TelegramBotPlatformOptions> _options;
    private readonly ILogger<StartCommandHandler> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="notifier">发送门面</param>
    /// <param name="options">平台选项监视器</param>
    /// <param name="logger">日志记录器</param>
    public StartCommandHandler(
        ITelegramNotifier notifier,
        IOptionsMonitor<TelegramBotPlatformOptions> options,
        ILogger<StartCommandHandler> logger)
    {
        _notifier = notifier;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 处理 /start 命令
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="args">命令参数（深链参数已由分发器前置消费，此处忽略）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task HandleAsync(TelegramBotContext context, string[] args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var botUsername = string.IsNullOrWhiteSpace(context.Bot.Username) ? context.Bot.Name : context.Bot.Username;
        var text = _options.CurrentValue.Texts.StartReply.Replace("{botUsername}", botUsername);

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
            // 发送门面自带重试退避，最终失败仅记日志，不打断分发
            _logger.LogWarning(ex, "Telegram /start 回复发送失败。Bot={BotName}, ChatId={ChatId}", context.Bot.Name, context.ChatId);
        }
    }
}
