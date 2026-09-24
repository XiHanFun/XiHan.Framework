// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
    Task SetUpgradingAsync(UpgradeVersionState version, string nodeName, DateTimeOffset startTime, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// 当前库还没有版本记录时，按给定版本登记一条
    /// </summary>
    /// <param name="appVersion">应用版本</param>
    /// <param name="dbVersion">数据库版本</param>
    /// <param name="minSupportVersion">最小支持版本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>新登记返回 true，已有记录返回 false（不改动既有记录）</returns>
    Task<bool> TryCreateBaselineAsync(string appVersion, string dbVersion, string minSupportVersion, CancellationToken cancellationToken = default);
}
