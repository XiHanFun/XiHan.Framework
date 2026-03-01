#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IUpgradeVersionStore
// Guid:3d4f0b44-21a8-4b19-9f3c-2f5a1f32b27c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 16:26:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Upgrade.Models;

namespace XiHan.Framework.Upgrade.Abstractions;

/// <summary>
/// 升级版本存储接口
/// </summary>
public interface IUpgradeVersionStore
{
    /// <summary>
    /// 确保升级相关表存在
    /// </summary>
    Task EnsureTablesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取或创建系统版本记录
    /// </summary>
    Task<UpgradeVersionState> GetOrCreateAsync(string currentAppVersion, string minSupportVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取最新迁移历史记录
    /// </summary>
    Task<UpgradeMigrationHistory?> GetLatestHistoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置升级中状态
    /// </summary>
    Task SetUpgradingAsync(UpgradeVersionState version, string nodeName, DateTime startTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置升级完成状态
    /// </summary>
    Task SetUpgradeCompletedAsync(UpgradeVersionState version, string appVersion, string dbVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置升级失败状态
    /// </summary>
    Task SetUpgradeFailedAsync(UpgradeVersionState version, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新数据库版本
    /// </summary>
    Task UpdateDbVersionAsync(UpgradeVersionState version, string dbVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// 追加迁移历史记录
    /// </summary>
    Task AddMigrationHistoryAsync(UpgradeMigrationHistory history, CancellationToken cancellationToken = default);

    /// <summary>
    /// 是否已执行指定脚本
    /// </summary>
    Task<bool> HasMigrationHistoryAsync(string version, string scriptName, CancellationToken cancellationToken = default);
}
