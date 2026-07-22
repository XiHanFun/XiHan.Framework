// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Bot.Consts;

/// <summary>
/// 消息 Data 键名常量
/// </summary>
/// <remarks>
/// 仅保留内核通用键；各提供者专属键由对应子包
/// （XiHan.Framework.Bot.Email / .DingTalk / .Lark / .WeCom / .Telegram）的 {X}MessageDataKeys 提供。
/// </remarks>
public static class BotMessageDataKeys
{
    /// <summary>
    /// 策略名称
    /// </summary>
    public const string Strategy = "Strategy";
}
