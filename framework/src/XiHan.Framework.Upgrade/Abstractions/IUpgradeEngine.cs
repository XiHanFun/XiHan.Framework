// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Upgrade.Models;

namespace XiHan.Framework.Upgrade.Abstractions;

/// <summary>
/// 升级引擎接口
/// </summary>
public interface IUpgradeEngine
{
    /// <summary>
    /// 执行升级
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<UpgradeStartResult> ExecuteAsync(CancellationToken cancellationToken = default);
}
