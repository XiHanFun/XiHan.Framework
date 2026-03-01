#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IUpgradeStatusService
// Guid:b56a2b97-5b87-4b5b-89ad-99e49d946129
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 16:25:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Upgrade.Enums;
using XiHan.Framework.Upgrade.Models;

namespace XiHan.Framework.Upgrade.Abstractions;

/// <summary>
/// 升级状态服务接口
/// </summary>
public interface IUpgradeStatusService
{
    /// <summary>
    /// 确保升级模块初始化
    /// </summary>
    /// <returns></returns>
    Task EnsureInitializedAsync();

    /// <summary>
    /// 获取升级版本快照
    /// </summary>
    /// <param name="clientVersion"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<UpgradeVersionSnapshot> GetVersionSnapshotAsync(string? clientVersion = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前升级状态
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<UpgradeStatus> GetUpgradeStatusAsync(CancellationToken cancellationToken = default);
}
