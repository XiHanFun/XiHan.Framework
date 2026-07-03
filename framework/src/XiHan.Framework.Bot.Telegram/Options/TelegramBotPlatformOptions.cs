#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TelegramBotPlatformOptions
// Guid:b2b0cc02-44a5-48de-b848-defb904b41ac
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Bot.Telegram.Options;

/// <summary>
/// Telegram 机器人平台选项（配置文件兜底；生产环境全局设置与机器人列表由应用层 store 覆盖）
/// </summary>
public class TelegramBotPlatformOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Bot:Telegram:Platform";

    /// <summary>
    /// 平台全局设置
    /// </summary>
    public TelegramBotSettings Settings { get; set; } = new();

    /// <summary>
    /// 机器人配置列表
    /// </summary>
    public List<TelegramBotConfig> Bots { get; set; } = [];

    /// <summary>
    /// 消息发送重试配置
    /// </summary>
    public TelegramBotRetryOptions Retry { get; set; } = new();

    /// <summary>
    /// 平台文案配置
    /// </summary>
    public TelegramBotTexts Texts { get; set; } = new();
}
