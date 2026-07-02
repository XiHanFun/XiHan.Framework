#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MyIdCommandHandler
// Guid:e0a4ab3f-ace2-48e2-b088-7f35fa740831
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/03 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Telegram.Core;
using XiHan.Framework.Bot.Telegram.Messaging;
using XiHan.Framework.Bot.Telegram.Options;

namespace XiHan.Framework.Bot.Telegram.Handlers.Builtin;

/// <summary>
/// 内置 /myid 命令处理器：回复当前会话 ChatId 与用户 UserId（<see cref="TelegramBotTexts.MyIdReply"/>，支持 {chatId} / {userId} 占位符）
/// </summary>
/// <remarks>
/// 须显式注册（AddTelegramBotBuiltinHandlers 或 AddTelegramBotHandler&lt;MyIdCommandHandler&gt;()）。
/// </remarks>
[BotCommand("/myid", Description = "查看我的 Telegram Id", Aliases = ["/id"])]
public sealed class MyIdCommandHandler : IBotCommandHandler
{
    private readonly ITelegramNotifier _notifier;
    private readonly IOptionsMonitor<TelegramBotPlatformOptions> _options;
    private readonly ILogger<MyIdCommandHandler> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="notifier">发送门面</param>
    /// <param name="options">平台选项监视器</param>
    /// <param name="logger">日志记录器</param>
    public MyIdCommandHandler(
        ITelegramNotifier notifier,
        IOptionsMonitor<TelegramBotPlatformOptions> options,
        ILogger<MyIdCommandHandler> logger)
    {
        _notifier = notifier;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 处理 /myid 命令
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="args">命令参数（忽略）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task HandleAsync(TelegramBotContext context, string[] args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var text = _options.CurrentValue.Texts.MyIdReply
            .Replace("{chatId}", context.ChatId.ToString(CultureInfo.InvariantCulture))
            .Replace("{userId}", context.UserId.ToString(CultureInfo.InvariantCulture));

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
            _logger.LogWarning(ex, "Telegram /myid 回复发送失败。Bot={BotName}, ChatId={ChatId}", context.Bot.Name, context.ChatId);
        }
    }
}
