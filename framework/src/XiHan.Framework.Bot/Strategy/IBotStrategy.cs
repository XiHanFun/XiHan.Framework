#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IBotStrategy
// Guid:685c6939-708d-4357-88dc-d33b789ea792
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 18:15:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Providers;

namespace XiHan.Framework.Bot.Strategy;

/// <summary>
/// Bot 策略抽象
/// </summary>
public interface IBotStrategy
{
    /// <summary>
    /// 策略名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 使用提供者执行策略
    /// </summary>
    Task ExecuteAsync(BotContext context, IReadOnlyList<IBotProvider> providers);
}
