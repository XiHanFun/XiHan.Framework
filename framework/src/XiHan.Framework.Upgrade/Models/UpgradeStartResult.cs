#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UpgradeStartResult
// Guid:7035a580-8e76-4a1c-a474-6c7c2f5e8b3b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 16:24:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Upgrade.Enums;

namespace XiHan.Framework.Upgrade.Models;

/// <summary>
/// 升级启动结果
/// </summary>
public sealed record UpgradeStartResult
{
    /// <summary>
    /// 是否已启动
    /// </summary>
    public bool Started { get; init; }

    /// <summary>
    /// 升级状态
    /// </summary>
    public UpgradeStatus Status { get; init; }

    /// <summary>
    /// 结果消息
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
