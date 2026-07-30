// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
