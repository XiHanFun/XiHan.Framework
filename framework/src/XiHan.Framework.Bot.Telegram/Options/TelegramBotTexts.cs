#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TelegramBotTexts
// Guid:12b3f5f3-09d8-4636-8806-237afa5a560f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Bot.Telegram.Options;

/// <summary>
/// Telegram 机器人平台文案配置（带中文默认值，应用层可整体覆盖）
/// </summary>
public class TelegramBotTexts
{
    /// <summary>
    /// 处理异常时的兜底回复文案
    /// </summary>
    public string InternalErrorReply { get; set; } = "系统处理异常，已记录日志，请稍后重试或联系管理员。";

    /// <summary>
    /// 命令未开启（不在命令白名单）时的回复文案
    /// </summary>
    public string CommandDisabledReply { get; set; } = "该命令未开启。";

    /// <summary>
    /// 非管理员执行管理员命令时的回复文案
    /// </summary>
    public string AdminOnlyCommandReply { get; set; } = "该命令仅管理员可用。";

    /// <summary>
    /// 非管理员点击管理员按钮时的回复文案
    /// </summary>
    public string AdminOnlyCallbackReply { get; set; } = "该操作仅管理员可用。";

    /// <summary>
    /// 无任何处理器命中普通消息时的兜底回复文案（需开启兜底回复开关）
    /// </summary>
    public string UnhandledMessageReply { get; set; } = "暂不支持处理该消息，发送 /help 查看可用命令。";

    /// <summary>
    /// 消息发送最终失败时通知管理员的告警标题
    /// </summary>
    public string SendFailureAdminNotifyTitle { get; set; } = "消息发送失败告警";
}
