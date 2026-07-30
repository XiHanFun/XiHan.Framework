// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
