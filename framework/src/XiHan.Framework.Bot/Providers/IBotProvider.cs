#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IBotProvider
// Guid:ad2091f6-1fee-4b22-8cf8-9e85a406be26
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 18:15:35
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Models;

namespace XiHan.Framework.Bot.Providers;

/// <summary>
/// Bot 提供者抽象
/// </summary>
public interface IBotProvider
{
    /// <summary>
    /// 提供者名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 由提供者发送消息
    /// </summary>
    Task<BotResult> SendAsync(BotMessage message, BotContext context);
}
