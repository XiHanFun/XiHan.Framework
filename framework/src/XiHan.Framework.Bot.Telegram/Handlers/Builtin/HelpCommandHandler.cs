#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HelpCommandHandler
// Guid:6fe3b8cb-6b17-45a7-b516-7fbc28bb141a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/03 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Telegram.Core;
using XiHan.Framework.Bot.Telegram.Messaging;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Routing;

namespace XiHan.Framework.Bot.Telegram.Handlers.Builtin;

/// <summary>
/// 内置 /help 命令处理器：列出对当前用户可见的命令（按「命令 - 描述」每行输出）
/// </summary>
/// <remarks>
/// 可见性与命令菜单同一套过滤逻辑（<see cref="TelegramBotHandlerCatalog.GetVisibleCommands"/>）：
/// 按该机器人的命令白名单（AllowedCommands）过滤，非管理员时排除 AdminOnly 命令。
/// 须显式注册（AddTelegramBotBuiltinHandlers 或 AddTelegramBotHandler&lt;HelpCommandHandler&gt;()）。
/// </remarks>
[BotCommand("/help", Description = "查看帮助", Aliases = ["/h"])]
public sealed class HelpCommandHandler : IBotCommandHandler
{
    private readonly TelegramBotHandlerCatalog _catalog;
    private readonly ITelegramNotifier _notifier;
    private readonly IOptionsMonitor<TelegramBotPlatformOptions> _options;
    private readonly ILogger<HelpCommandHandler> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="catalog">处理器目录</param>
    /// <param name="notifier">发送门面</param>
    /// <param name="options">平台选项监视器</param>
    /// <param name="logger">日志记录器</param>
    public HelpCommandHandler(
        TelegramBotHandlerCatalog catalog,
        ITelegramNotifier notifier,
        IOptionsMonitor<TelegramBotPlatformOptions> options,
        ILogger<HelpCommandHandler> logger)
    {
        _catalog = catalog;
        _notifier = notifier;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 处理 /help 命令
    /// </summary>
    /// <param name="context">更新上下文</param>
    /// <param name="args">命令参数（忽略）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task HandleAsync(TelegramBotContext context, string[] args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var texts = _options.CurrentValue.Texts;
        var descriptors = _catalog.GetVisibleCommands(context.Bot.Config.AllowedCommands, includeAdminOnly: context.IsAdmin);

        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(texts.HelpHeader))
        {
            _ = builder.AppendLine(texts.HelpHeader);
        }

        foreach (var descriptor in descriptors)
        {
            _ = builder.AppendLine(string.IsNullOrWhiteSpace(descriptor.Description)
                ? descriptor.Command
                : $"{descriptor.Command} - {descriptor.Description}");
        }

        var text = builder.ToString().TrimEnd();
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
            _logger.LogWarning(ex, "Telegram /help 回复发送失败。Bot={BotName}, ChatId={ChatId}", context.Bot.Name, context.ChatId);
        }
    }
}
