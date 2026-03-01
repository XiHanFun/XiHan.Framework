#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IUpgradeMigrationExecutor
// Guid:6d8d4f14-8f36-4f0a-93d8-4b99f58c879a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 17:10:20
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
