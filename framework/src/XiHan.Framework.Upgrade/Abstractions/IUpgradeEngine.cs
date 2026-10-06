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

    /// <summary>
    /// 把当前库登记为已处于最新脚本版本
    /// </summary>
    /// <remarks>
    /// 按当前实体刚建出来的库本就是最新结构，不该再从 0.0.0 补跑历史脚本——历史脚本改的表可能根本不在这个库里
    /// （库隔离租户的独立库只有租户数据的表）。只在当前库还没有版本记录时登记；已有记录说明它被升级引擎管着，保持不动。
    /// </remarks>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>新登记返回 true，已有版本记录返回 false</returns>
    Task<bool> BaselineAsync(CancellationToken cancellationToken = default);
}
