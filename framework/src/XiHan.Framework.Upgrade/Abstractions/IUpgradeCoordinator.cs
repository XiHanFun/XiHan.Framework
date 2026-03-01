#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IUpgradeCoordinator
// Guid:f070e0ff-6a1c-4fa7-bb6d-2d1d0e9a61b1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 16:27:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Upgrade.Models;

namespace XiHan.Framework.Upgrade.Abstractions;

/// <summary>
/// 升级协调器接口
/// </summary>
public interface IUpgradeCoordinator
{
    /// <summary>
    /// 启动升级（后台执行）
    /// </summary>
    /// <returns></returns>
    Task<UpgradeStartResult> StartAsync();
}
