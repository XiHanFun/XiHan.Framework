#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BotDispatchResult
// Guid:f966c0be-627b-4c40-860a-24f6167c0761
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:43:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Bot.Models;

/// <summary>
/// 提供者调度结果
/// </summary>
public sealed class BotDispatchResult
{
    /// <summary>
    /// 提供者名称
    /// </summary>
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>
    /// 结果
    /// </summary>
    public BotResult Result { get; init; } = new();
}
