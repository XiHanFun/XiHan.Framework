// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Upgrade.Abstractions;

/// <summary>
/// 升级迁移执行器接口
/// </summary>
public interface IUpgradeMigrationExecutor
{
    /// <summary>
    /// 执行迁移脚本（内部保证事务）
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task ExecuteAsync(string sql, CancellationToken cancellationToken = default);
}
