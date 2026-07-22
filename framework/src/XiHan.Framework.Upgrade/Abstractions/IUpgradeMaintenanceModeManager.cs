// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Upgrade.Abstractions;

/// <summary>
/// 维护模式管理器接口
/// </summary>
public interface IUpgradeMaintenanceModeManager
{
    /// <summary>
    /// 进入维护模式
    /// </summary>
    Task EnterAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 退出维护模式
    /// </summary>
    Task ExitAsync(CancellationToken cancellationToken = default);
}
