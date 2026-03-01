#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IUpgradeMaintenanceModeManager
// Guid:5b0c0a2a-7f86-4c0a-b7ab-2e2adba68fda
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 16:26:20
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
