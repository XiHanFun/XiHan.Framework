#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TelegramBotHandlerCatalog
// Guid:677fc50c-dc5b-4498-92fc-7ffe1dfa7ce6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Telegram.Core;
using XiHan.Framework.Bot.Telegram.Handlers;
using XiHan.Framework.Bot.Telegram.Options;

namespace XiHan.Framework.Bot.Telegram.Routing;

/// <summary>
/// 命令路由项
/// </summary>
/// <param name="HandlerType">处理器类型</param>
/// <param name="AdminOnly">是否仅管理员可执行</param>
/// <param name="NormalizedCommands">归一化命令集合（主命令+别名）</param>
/// <param name="IsAlwaysAvailable">是否永久放行</param>
public sealed record TelegramCommandRoute(Type HandlerType, bool AdminOnly, string[] NormalizedCommands, bool IsAlwaysAvailable);

/// <summary>
/// 命令正则路由项
/// </summary>
/// <param name="Route">命令路由</param>
/// <param name="Regex">匹配正则</param>
public sealed record TelegramCommandPatternRoute(TelegramCommandRoute Route, Regex Regex);

/// <summary>
/// 回调路由项
/// </summary>
/// <param name="HandlerType">处理器类型</param>
/// <param name="AdminOnly">是否仅管理员可执行</param>
public sealed record TelegramCallbackRoute(Type HandlerType, bool AdminOnly);

/// <summary>
/// Telegram 机器人处理器目录（从显式注册的 <see cref="TelegramBotHandlerOptions"/> 构建路由表）
/// </summary>
/// <remarks>
/// 不做程序集反射扫描；注册非法（缺属性、重复命令/动作、未实现处理器接口）在构建时抛异常快速失败。
/// </remarks>
public sealed class TelegramBotHandlerCatalog
{
    private readonly Dictionary<string, TelegramCommandRoute> _commandRoutes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TelegramCommandPatternRoute> _commandPatternRoutes = [];
    private readonly Dictionary<string, TelegramCallbackRoute> _callbackRoutes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CommandDescriptor> _commandDescriptors = [];
    private readonly List<Type> _messageHandlerTypes = [];
    private readonly List<Type> _replyHandlerTypes = [];
    private readonly List<Type> _stateHandlerTypes = [];
    private readonly List<Type> _inlineQueryHandlerTypes = [];
    private readonly List<Type> _startPayloadHandlerTypes = [];

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">处理器注册选项</param>
    public TelegramBotHandlerCatalog(IOptions<TelegramBotHandlerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        foreach (var handlerType in options.Value.Handlers.Distinct())
        {
            RegisterHandlerType(handlerType);
        }
    }

    /// <summary>
    /// 命令路由表（归一化命令 → 路由）
    /// </summary>
    public IReadOnlyDictionary<string, TelegramCommandRoute> CommandRoutes => _commandRoutes;

    /// <summary>
    /// 命令正则路由列表
    /// </summary>
    public IReadOnlyList<TelegramCommandPatternRoute> CommandPatternRoutes => _commandPatternRoutes;

    /// <summary>
    /// 回调路由表（Action → 路由）
    /// </summary>
    public IReadOnlyDictionary<string, TelegramCallbackRoute> CallbackRoutes => _callbackRoutes;

    /// <summary>
    /// 普通消息处理器类型列表
    /// </summary>
    public IReadOnlyList<Type> MessageHandlerTypes => _messageHandlerTypes;

    /// <summary>
    /// 回复消息处理器类型列表
    /// </summary>
    public IReadOnlyList<Type> ReplyHandlerTypes => _replyHandlerTypes;

    /// <summary>
    /// 会话状态处理器类型列表
    /// </summary>
    public IReadOnlyList<Type> StateHandlerTypes => _stateHandlerTypes;

    /// <summary>
    /// 内联查询处理器类型列表
    /// </summary>
    public IReadOnlyList<Type> InlineQueryHandlerTypes => _inlineQueryHandlerTypes;

    /// <summary>
    /// /start 深链参数处理器类型列表
    /// </summary>
    public IReadOnlyList<Type> StartPayloadHandlerTypes => _startPayloadHandlerTypes;

    /// <summary>
    /// 获取可注册到 Telegram 命令菜单的公开命令列表（排除 AdminOnly，按命令白名单过滤）
    /// </summary>
    /// <param name="allowedCommands">命令白名单（null 或空表示不限制）</param>
    /// <param name="preferAliasDescription">是否优先使用别名作为描述（群组菜单常用）</param>
    /// <returns>Telegram 菜单命令列表</returns>
    public IReadOnlyList<global::Telegram.Bot.Types.BotCommand> GetPublicCommands(
        IEnumerable<string>? allowedCommands = null,
        bool preferAliasDescription = false)
    {
        var allowedCommandSet = BuildAllowedCommandSet(allowedCommands);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var commands = new List<global::Telegram.Bot.Types.BotCommand>();

        foreach (var descriptor in _commandDescriptors)
        {
            if (descriptor.AdminOnly)
            {
                continue;
            }

            if (!IsMenuRouteAllowed(descriptor.NormalizedCommands, allowedCommandSet) || !seen.Add(descriptor.Command))
            {
                continue;
            }

            commands.Add(new global::Telegram.Bot.Types.BotCommand
            {
                Command = descriptor.Command.TrimStart('/'),
                Description = BuildCommandDescription(descriptor, preferAliasDescription)
            });
        }

        return commands;
    }

    private void RegisterHandlerType(Type handlerType)
    {
        ArgumentNullException.ThrowIfNull(handlerType);

        var matched = false;

        if (typeof(IBotCommandHandler).IsAssignableFrom(handlerType))
        {
            RegisterCommandHandler(handlerType);
            matched = true;
        }

        if (typeof(IBotCallbackHandler).IsAssignableFrom(handlerType))
        {
            RegisterCallbackHandler(handlerType);
            matched = true;
        }

        if (typeof(IBotMessageHandler).IsAssignableFrom(handlerType))
        {
            _messageHandlerTypes.Add(handlerType);
            matched = true;
        }

        if (typeof(IBotReplyHandler).IsAssignableFrom(handlerType))
        {
            _replyHandlerTypes.Add(handlerType);
            matched = true;
        }

        if (typeof(IBotStateHandler).IsAssignableFrom(handlerType))
        {
            _stateHandlerTypes.Add(handlerType);
            matched = true;
        }

        if (typeof(IBotInlineQueryHandler).IsAssignableFrom(handlerType))
        {
            _inlineQueryHandlerTypes.Add(handlerType);
            matched = true;
        }

        if (typeof(IBotStartPayloadHandler).IsAssignableFrom(handlerType))
        {
            _startPayloadHandlerTypes.Add(handlerType);
            matched = true;
        }

        if (!matched)
        {
            throw new InvalidOperationException(
                $"Telegram Bot 处理器注册非法：{handlerType.FullName} 未实现任何 IBot*Handler 接口。");
        }
    }

    private void RegisterCommandHandler(Type handlerType)
    {
        var attrs = handlerType.GetCustomAttributes<BotCommandAttribute>(inherit: false).ToArray();
        if (attrs.Length == 0)
        {
            throw new InvalidOperationException(
                $"Telegram Bot 命令处理器注册非法：{handlerType.FullName} 缺少 [BotCommand] 属性。");
        }

        foreach (var attr in attrs)
        {
            var command = TelegramCommandGuards.NormalizeCommandToken(attr.Command)!;
            var aliases = attr.GetNormalizedAliases();
            var normalizedCommands = BuildNormalizedCommands(command, aliases);
            var route = new TelegramCommandRoute(
                handlerType,
                attr.AdminOnly,
                normalizedCommands,
                TelegramCommandGuards.IsAlwaysAvailableRoute(normalizedCommands));

            AddCommandRoute(command, route, handlerType);
            foreach (var alias in aliases)
            {
                AddCommandRoute(alias, route, handlerType);
            }

            var regex = attr.BuildRegex();
            if (regex is not null)
            {
                _commandPatternRoutes.Add(new TelegramCommandPatternRoute(route, regex));
            }

            _commandDescriptors.Add(new CommandDescriptor(
                command,
                attr.Description?.Trim() ?? string.Empty,
                attr.AdminOnly,
                aliases,
                normalizedCommands));
        }
    }

    private void RegisterCallbackHandler(Type handlerType)
    {
        var attrs = handlerType.GetCustomAttributes<BotCallbackAttribute>(inherit: false).ToArray();
        if (attrs.Length == 0)
        {
            throw new InvalidOperationException(
                $"Telegram Bot 回调处理器注册非法：{handlerType.FullName} 缺少 [BotCallback] 属性。");
        }

        foreach (var attr in attrs)
        {
            if (_callbackRoutes.TryGetValue(attr.Action, out var existing))
            {
                throw new InvalidOperationException(
                    $"Telegram Bot 回调动作重复：{attr.Action}（{existing.HandlerType.FullName} 与 {handlerType.FullName}）。");
            }

            _callbackRoutes[attr.Action] = new TelegramCallbackRoute(handlerType, attr.AdminOnly);
        }
    }

    private void AddCommandRoute(string command, TelegramCommandRoute route, Type handlerType)
    {
        if (_commandRoutes.TryGetValue(command, out var existing))
        {
            throw new InvalidOperationException(
                $"Telegram Bot 命令重复：{command}（{existing.HandlerType.FullName} 与 {handlerType.FullName}）。");
        }

        _commandRoutes[command] = route;
    }

    private static string[] BuildNormalizedCommands(string command, IEnumerable<string> aliases)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { command };
        foreach (var alias in aliases)
        {
            _ = values.Add(alias);
        }

        return [.. values];
    }

    private static HashSet<string>? BuildAllowedCommandSet(IEnumerable<string>? allowedCommands)
    {
        if (allowedCommands is null)
        {
            return null;
        }

        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var allowedCommand in allowedCommands)
        {
            var normalized = TelegramCommandGuards.NormalizeCommandToken(allowedCommand);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                _ = values.Add(normalized);
            }
        }

        return values.Count == 0 ? null : values;
    }

    private static bool IsMenuRouteAllowed(string[] normalizedCommands, HashSet<string>? allowedCommandSet)
    {
        // 菜单与运行时守卫保持一致：永久放行命令仅豁免群组/频道白名单守卫，命令白名单同样约束其菜单展示
        if (allowedCommandSet is null)
        {
            return true;
        }

        return normalizedCommands.Any(allowedCommandSet.Contains);
    }

    private static string BuildCommandDescription(CommandDescriptor descriptor, bool preferAliasDescription)
    {
        var normalizedCommand = descriptor.Command.TrimStart('/');

        if (!string.IsNullOrWhiteSpace(descriptor.Description))
        {
            // Telegram 要求描述至少 3 个字符，过短时附带命令名兜底
            return descriptor.Description.Length >= 3
                ? descriptor.Description
                : $"{descriptor.Description} / {normalizedCommand}";
        }

        if (!preferAliasDescription || descriptor.Aliases.Length == 0)
        {
            return normalizedCommand;
        }

        var alias = descriptor.Aliases[0].TrimStart('/');
        return alias.Length >= 3 ? alias : $"{alias} / {normalizedCommand}";
    }

    private sealed record CommandDescriptor(
        string Command,
        string Description,
        bool AdminOnly,
        string[] Aliases,
        string[] NormalizedCommands);
}
