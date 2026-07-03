#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BotMessageDataKeys
// Guid:daa27d39-1359-423d-9100-9134edb3531d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:44:03
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
