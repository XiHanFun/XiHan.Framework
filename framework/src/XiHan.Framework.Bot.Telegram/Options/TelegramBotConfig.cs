// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Bot.Telegram.Options;

/// <summary>
/// 单个 Telegram 机器人的配置项
/// </summary>
public class TelegramBotConfig
{
    /// <summary>
    /// 配置 Id（数据库来源可用，仅用于变更检测与审计定位）
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 机器人名称（业务侧以该名称定位机器人，不可重复）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Bot Token
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 超级管理员 Telegram 用户 Id 列表（可选）
    /// </summary>
    public long[] AdminUsers { get; set; } = [];

    /// <summary>
    /// 允许使用该机器人的群组 ChatId 白名单。
    /// <para><b>注意：fail-closed 语义——为空表示拒收所有群组消息</b>（与直觉「空=不限制」相反）；
    /// 仅永久放行命令（/start /myid /help）不受该白名单限制。私聊消息不受此项影响。</para>
    /// </summary>
    public long[] AllowedGroupChatIds { get; set; } = [];

    /// <summary>
    /// 允许执行的命令白名单（为空表示不限制命令）
    /// </summary>
    public string[] AllowedCommands { get; set; } = [];

    /// <summary>
    /// 是否启用兜底回复（与平台全局设置任一开启即生效）
    /// </summary>
    public bool EnableFallbackReply { get; set; }

    /// <summary>
    /// 备注（可选）
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 比较配置是否一致（Name / Token / Id / AdminUsers / AllowedGroupChatIds / AllowedCommands），
    /// 不一致时管理器会重启该机器人
    /// </summary>
    /// <param name="other">对比配置</param>
    /// <returns>是否一致</returns>
    public bool IsSameAs(TelegramBotConfig? other)
    {
        if (other is null)
        {
            return false;
        }

        if (!string.Equals(Name?.Trim(), other.Name?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(Token?.Trim(), other.Token?.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        if (Id != other.Id)
        {
            return false;
        }

        if (EnableFallbackReply != other.EnableFallbackReply)
        {
            return false;
        }

        var leftAdmins = (AdminUsers ?? []).Where(x => x > 0).Distinct().OrderBy(x => x).ToArray();
        var rightAdmins = (other.AdminUsers ?? []).Where(x => x > 0).Distinct().OrderBy(x => x).ToArray();
        if (!leftAdmins.SequenceEqual(rightAdmins))
        {
            return false;
        }

        var leftGroups = (AllowedGroupChatIds ?? []).Where(x => x != 0).Distinct().OrderBy(x => x).ToArray();
        var rightGroups = (other.AllowedGroupChatIds ?? []).Where(x => x != 0).Distinct().OrderBy(x => x).ToArray();
        if (!leftGroups.SequenceEqual(rightGroups))
        {
            return false;
        }

        var leftCommands = NormalizeCommands(AllowedCommands);
        var rightCommands = NormalizeCommands(other.AllowedCommands);
        return leftCommands.SequenceEqual(rightCommands, StringComparer.OrdinalIgnoreCase);
    }

    private static string[] NormalizeCommands(string[]? commands)
    {
        return [.. (commands ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
    }
}
