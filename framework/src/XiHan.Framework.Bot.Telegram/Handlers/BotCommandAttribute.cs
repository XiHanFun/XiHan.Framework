// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.RegularExpressions;

namespace XiHan.Framework.Bot.Telegram.Handlers;

/// <summary>
/// 标记命令处理器绑定的命令（用于 <see cref="IBotCommandHandler"/>）
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class BotCommandAttribute : Attribute
{
    /// <summary>
    /// 创建命令标记
    /// </summary>
    /// <param name="command">命令文本（含或不含 / 前缀均可）</param>
    public BotCommandAttribute(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Command 不能为空。", nameof(command));
        }

        var value = command.Trim();
        Command = value.StartsWith('/') ? value : "/" + value;
    }

    /// <summary>
    /// 命令文本（已归一为 / 前缀）
    /// </summary>
    public string Command { get; }

    /// <summary>
    /// 注册到 Telegram 命令菜单时展示的描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 是否仅管理员可执行
    /// </summary>
    public bool AdminOnly { get; set; }

    /// <summary>
    /// 命令简写别名（如 /o）
    /// </summary>
    public string[] Aliases { get; set; } = [];

    /// <summary>
    /// 文本正则匹配（非命令文本命中后也会路由到该处理器，捕获组作为参数）
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// 获取归一化后的别名列表
    /// </summary>
    /// <returns>别名列表</returns>
    public string[] GetNormalizedAliases()
    {
        return [.. (Aliases ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeCommand)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// 构建文本正则（带 100ms 超时防 ReDoS）
    /// </summary>
    /// <returns>正则；未配置时为 null</returns>
    public Regex? BuildRegex()
    {
        if (string.IsNullOrWhiteSpace(Pattern))
        {
            return null;
        }

        return new Regex(Pattern.Trim(), RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }

    private static string NormalizeCommand(string command)
    {
        var value = command.Trim();
        return value.StartsWith('/') ? value : "/" + value;
    }
}
