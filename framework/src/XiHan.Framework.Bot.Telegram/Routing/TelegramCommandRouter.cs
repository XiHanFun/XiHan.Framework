// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Telegram.Core;
using XiHan.Framework.Bot.Telegram.Handlers;
using XiHan.Framework.Bot.Telegram.Messaging;
using XiHan.Framework.Bot.Telegram.Options;

namespace XiHan.Framework.Bot.Telegram.Routing;

/// <summary>
/// 命令路由器：把 /command（含正则直达文本）分发到标注了 <see cref="BotCommandAttribute"/> 的处理器
/// </summary>
public sealed class TelegramCommandRouter
{
    private readonly TelegramBotHandlerCatalog _catalog;
    private readonly ITelegramNotifier _notifier;
    private readonly IOptionsMonitor<TelegramBotPlatformOptions> _options;
    private readonly ILogger<TelegramCommandRouter> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="catalog">处理器目录</param>
    /// <param name="notifier">发送门面</param>
    /// <param name="options">平台选项监视器</param>
    /// <param name="logger">日志记录器</param>
    public TelegramCommandRouter(
        TelegramBotHandlerCatalog catalog,
        ITelegramNotifier notifier,
        IOptionsMonitor<TelegramBotPlatformOptions> options,
        ILogger<TelegramCommandRouter> logger)
    {
        _catalog = catalog;
        _notifier = notifier;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 处理命令消息
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

        var resolved = ResolveRoute(context);
        if (resolved is null)
        {
            return false;
        }

        var (route, args) = resolved.Value;
        var texts = _options.CurrentValue.Texts;

        // 永久放行命令仅豁免分发器中的群组/频道白名单守卫；命令白名单与 AdminOnly 校验一律执行
        if (!IsRouteAllowed(context, route.NormalizedCommands))
        {
            await TryReplyAsync(context, texts.CommandDisabledReply, cancellationToken);
            return true;
        }

        if (route.AdminOnly && !context.IsAdmin)
        {
            await TryReplyAsync(context, texts.AdminOnlyCommandReply, cancellationToken);
            return true;
        }

        if (scopedProvider.GetService(route.HandlerType) is not IBotCommandHandler handler)
        {
            _logger.LogError(
                "Telegram 命令处理器未在 DI 注册：{HandlerType}（请使用 AddTelegramBotHandler<THandler>() 注册）。Bot={BotName}",
                route.HandlerType.FullName, context.Bot.Name);
            return false;
        }

        await handler.HandleAsync(context, args, cancellationToken);
        return true;
    }

    private static bool IsRouteAllowed(TelegramBotContext context, string[] normalizedCommands)
    {
        var allowedCommands = context.Bot.Config.AllowedCommands ?? [];
        if (allowedCommands.Length == 0)
        {
            return true;
        }

        var allowedSet = allowedCommands
            .Select(TelegramCommandGuards.NormalizeCommandToken)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return normalizedCommands.Any(x => allowedSet.Contains(x));
    }

    private static string[] BuildRegexArgs(Match match, string originalText)
    {
        if (match.Groups.Count <= 1)
        {
            var value = string.IsNullOrWhiteSpace(match.Value) ? originalText : match.Value.Trim();
            return [value];
        }

        var values = new List<string>();
        for (var i = 1; i < match.Groups.Count; i++)
        {
            var value = match.Groups[i].Value?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }

        return values.Count > 0 ? [.. values] : [originalText];
    }

    private (TelegramCommandRoute Route, string[] Args)? ResolveRoute(TelegramBotContext context)
    {
        var rawToken = context.GetCommandToken();
        if (!string.IsNullOrWhiteSpace(rawToken))
        {
            var command = TelegramCommandGuards.NormalizeCommandToken(rawToken)!;
            if (_catalog.CommandRoutes.TryGetValue(command, out var route))
            {
                return (route, context.GetCommandArgs());
            }
        }

        var text = context.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        foreach (var patternRoute in _catalog.CommandPatternRoutes)
        {
            Match match;
            try
            {
                match = patternRoute.Regex.Match(text);
            }
            catch (RegexMatchTimeoutException)
            {
                continue;
            }

            if (!match.Success)
            {
                continue;
            }

            return (patternRoute.Route, BuildRegexArgs(match, text));
        }

        return null;
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
            _logger.LogWarning(ex, "Telegram 命令守卫回复发送失败。Bot={BotName}, ChatId={ChatId}", context.Bot.Name, context.ChatId);
        }
    }
}
