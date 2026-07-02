#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TelegramCommandGuards
// Guid:1670db33-d0a4-45bc-91c4-99c776ac67ae
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Bot.Telegram.Platform.Core;

/// <summary>
/// Telegram 命令放行规则
/// </summary>
public static class TelegramCommandGuards
{
    /// <summary>
    /// 永久放行的命令（仅豁免群组/频道白名单守卫；命令白名单与管理员限制仍然生效）
    /// </summary>
    private static readonly HashSet<string> AlwaysAvailableCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "/start",
        "/myid", "/id",
        "/help", "/h"
    };

    /// <summary>
    /// 判断命令 token 是否属于永久放行命令
    /// </summary>
    /// <param name="commandToken">命令 token（可含 @bot 后缀）</param>
    /// <returns>是否永久放行</returns>
    public static bool IsAlwaysAvailableCommandToken(string? commandToken)
    {
        var normalized = NormalizeCommandToken(commandToken);
        return normalized is not null && AlwaysAvailableCommands.Contains(normalized);
    }

    /// <summary>
    /// 判断命令路由是否属于永久放行命令
    /// </summary>
    /// <param name="normalizedCommands">路由绑定的归一化命令集合</param>
    /// <returns>是否永久放行</returns>
    public static bool IsAlwaysAvailableRoute(IEnumerable<string>? normalizedCommands)
    {
        return normalizedCommands is not null && normalizedCommands.Any(IsAlwaysAvailableCommandToken);
    }

    /// <summary>
    /// 归一化命令 token（去掉 @bot 后缀并补齐前导斜杠）
    /// </summary>
    /// <param name="commandToken">命令 token</param>
    /// <returns>归一化命令；空输入时为 null</returns>
    public static string? NormalizeCommandToken(string? commandToken)
    {
        if (string.IsNullOrWhiteSpace(commandToken))
        {
            return null;
        }

        var value = commandToken.Trim();
        var atIndex = value.IndexOf('@');
        if (atIndex >= 0)
        {
            value = value[..atIndex];
        }

        if (!value.StartsWith('/'))
        {
            value = "/" + value;
        }

        return value;
    }
}
