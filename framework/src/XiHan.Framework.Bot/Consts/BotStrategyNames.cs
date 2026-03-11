#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BotStrategyNames
// Guid:a6248d7c-7196-4aa2-9d1b-1707aed9bb5a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:44:03
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Bot.Consts;

/// <summary>
/// 策略名称常量
/// </summary>
public static class BotStrategyNames
{
    /// <summary>
    /// 广播策略：向所有提供者发送
    /// </summary>
    public const string Broadcast = "Broadcast";

    /// <summary>
    /// 主备策略：按顺序发送，成功即止
    /// </summary>
    public const string Failover = "Failover";

    /// <summary>
    /// 优先级策略：仅发往第一个提供者
    /// </summary>
    public const string Priority = "Priority";
}
