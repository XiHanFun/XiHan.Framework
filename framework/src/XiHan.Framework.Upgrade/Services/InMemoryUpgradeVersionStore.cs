#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:InMemoryUpgradeVersionStore
// Guid:7b2a0f3d-63d7-4f74-a760-a39dbcf11be6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/10 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Upgrade.Abstractions;
using XiHan.Framework.Upgrade.Models;

namespace XiHan.Framework.Upgrade.Services;

/// <summary>
/// 基于内存的升级版本存储（默认实现）
/// </summary>
public class InMemoryUpgradeVersionStore : IUpgradeVersionStore
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, UpgradeVersionState> VersionStates = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, List<UpgradeMigrationHistory>> MigrationHistories = new(StringComparer.Ordinal);
    private static long _idSeed;

    private readonly ICurrentTenant? _currentTenant;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="currentTenant">当前租户（可选）</param>
    public InMemoryUpgradeVersionStore(ICurrentTenant? currentTenant = null)
    {
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// 确保升级相关表存在（内存实现无需初始化）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task EnsureTablesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取或创建系统版本记录
    /// </summary>
    /// <param name="currentAppVersion">当前应用版本</param>
    /// <param name="minSupportVersion">最小支持版本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>版本状态</returns>
    public Task<UpgradeVersionState> GetOrCreateAsync(string currentAppVersion, string minSupportVersion, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tenantId = _currentTenant?.Id;
        var tenantKey = BuildTenantKey(tenantId);
        lock (SyncRoot)
        {
            if (!VersionStates.TryGetValue(tenantKey, out var state))
            {
                state = new UpgradeVersionState
                {
                    Id = Interlocked.Increment(ref _idSeed),
                    TenantId = tenantId,
                    AppVersion = NormalizeVersion(currentAppVersion),
                    DbVersion = "0.0.0",
                    MinSupportVersion = NormalizeVersion(minSupportVersion),
                    IsUpgrading = false
                };

                VersionStates[tenantKey] = state;
            }
            else
            {
                state.TenantId ??= tenantId;
                if (string.IsNullOrWhiteSpace(state.AppVersion))
                {
                    state.AppVersion = NormalizeVersion(currentAppVersion);
                }

                if (string.IsNullOrWhiteSpace(state.MinSupportVersion))
                {
                    state.MinSupportVersion = NormalizeVersion(minSupportVersion);
                }
            }

            return Task.FromResult(CloneState(state));
        }
    }

    /// <summary>
    /// 获取最新迁移历史记录
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>迁移历史</returns>
    public Task<UpgradeMigrationHistory?> GetLatestHistoryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tenantKey = BuildTenantKey(_currentTenant?.Id);
        lock (SyncRoot)
        {
            if (!MigrationHistories.TryGetValue(tenantKey, out var histories) || histories.Count == 0)
            {
                return Task.FromResult<UpgradeMigrationHistory?>(null);
            }

            var latest = histories
                .OrderByDescending(static x => x.ExecutedTime)
                .First();

            return Task.FromResult<UpgradeMigrationHistory?>(CloneHistory(latest));
        }
    }

    /// <summary>
    /// 设置升级中状态
    /// </summary>
    /// <param name="version">版本状态</param>
    /// <param name="nodeName">升级节点</param>
    /// <param name="startTime">开始时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task SetUpgradingAsync(UpgradeVersionState version, string nodeName, DateTimeOffset startTime, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(version);

        lock (SyncRoot)
        {
            var state = GetOrCreateStateForWrite(version);
            state.IsUpgrading = true;
            state.UpgradeNode = nodeName;
            state.UpgradeStartTime = startTime;
            state.TenantId ??= _currentTenant?.Id;

            CopyState(state, version);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 设置升级完成状态
    /// </summary>
    /// <param name="version">版本状态</param>
    /// <param name="appVersion">应用版本</param>
    /// <param name="dbVersion">数据库版本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task SetUpgradeCompletedAsync(UpgradeVersionState version, string appVersion, string dbVersion, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(version);

        lock (SyncRoot)
        {
            var state = GetOrCreateStateForWrite(version);
            state.IsUpgrading = false;
            state.AppVersion = NormalizeVersion(appVersion);
            state.DbVersion = NormalizeVersion(dbVersion);
            state.TenantId ??= _currentTenant?.Id;

            CopyState(state, version);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 设置升级失败状态
    /// </summary>
    /// <param name="version">版本状态</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task SetUpgradeFailedAsync(UpgradeVersionState version, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(version);

        lock (SyncRoot)
        {
            var state = GetOrCreateStateForWrite(version);
            state.IsUpgrading = false;
            state.TenantId ??= _currentTenant?.Id;

            CopyState(state, version);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 更新数据库版本
    /// </summary>
    /// <param name="version">版本状态</param>
    /// <param name="dbVersion">数据库版本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task UpdateDbVersionAsync(UpgradeVersionState version, string dbVersion, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(version);

        lock (SyncRoot)
        {
            var state = GetOrCreateStateForWrite(version);
            state.DbVersion = NormalizeVersion(dbVersion);
            state.TenantId ??= _currentTenant?.Id;

            CopyState(state, version);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 追加迁移历史记录
    /// </summary>
    /// <param name="history">迁移历史</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task AddMigrationHistoryAsync(UpgradeMigrationHistory history, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(history);

        var tenantId = history.TenantId ?? _currentTenant?.Id;
        var tenantKey = BuildTenantKey(tenantId);
        lock (SyncRoot)
        {
            if (!MigrationHistories.TryGetValue(tenantKey, out var histories))
            {
                histories = new List<UpgradeMigrationHistory>();
                MigrationHistories[tenantKey] = histories;
            }

            var snapshot = CloneHistory(history);
            snapshot.TenantId = tenantId;
            snapshot.Version = NormalizeVersion(snapshot.Version);
            histories.Add(snapshot);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 是否已执行指定脚本（仅成功执行视为已执行）
    /// </summary>
    /// <param name="version">版本</param>
    /// <param name="scriptName">脚本名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否已执行</returns>
    public Task<bool> HasMigrationHistoryAsync(string version, string scriptName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(scriptName))
        {
            return Task.FromResult(false);
        }

        var tenantKey = BuildTenantKey(_currentTenant?.Id);
        lock (SyncRoot)
        {
            if (!MigrationHistories.TryGetValue(tenantKey, out var histories) || histories.Count == 0)
            {
                return Task.FromResult(false);
            }

            var exists = histories.Any(x =>
                x.Success &&
                string.Equals(x.Version, version, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.ScriptName, scriptName, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(exists);
        }
    }

    /// <summary>
    /// 构建租户键
    /// </summary>
    /// <param name="tenantId">租户Id</param>
    /// <returns>租户键</returns>
    private static string BuildTenantKey(long? tenantId)
    {
        return tenantId.HasValue ? $"tenant:{tenantId.Value}" : "host";
    }

    /// <summary>
    /// 获取可写版本状态
    /// </summary>
    /// <param name="source">输入状态</param>
    /// <returns>可写状态</returns>
    private UpgradeVersionState GetOrCreateStateForWrite(UpgradeVersionState source)
    {
        var tenantId = source.TenantId ?? _currentTenant?.Id;
        var tenantKey = BuildTenantKey(tenantId);

        if (!VersionStates.TryGetValue(tenantKey, out var state))
        {
            state = new UpgradeVersionState
            {
                Id = source.Id > 0 ? source.Id : Interlocked.Increment(ref _idSeed),
                TenantId = tenantId,
                AppVersion = NormalizeVersion(source.AppVersion),
                DbVersion = NormalizeVersion(source.DbVersion),
                MinSupportVersion = NormalizeVersion(source.MinSupportVersion),
                IsUpgrading = source.IsUpgrading,
                UpgradeNode = source.UpgradeNode,
                UpgradeStartTime = source.UpgradeStartTime
            };

            VersionStates[tenantKey] = state;
        }

        return state;
    }

    /// <summary>
    /// 复制状态
    /// </summary>
    /// <param name="source">源状态</param>
    /// <param name="target">目标状态</param>
    private static void CopyState(UpgradeVersionState source, UpgradeVersionState target)
    {
        target.Id = source.Id;
        target.TenantId = source.TenantId;
        target.AppVersion = source.AppVersion;
        target.DbVersion = source.DbVersion;
        target.MinSupportVersion = source.MinSupportVersion;
        target.IsUpgrading = source.IsUpgrading;
        target.UpgradeNode = source.UpgradeNode;
        target.UpgradeStartTime = source.UpgradeStartTime;
    }

    /// <summary>
    /// 克隆状态
    /// </summary>
    /// <param name="source">源状态</param>
    /// <returns>克隆结果</returns>
    private static UpgradeVersionState CloneState(UpgradeVersionState source)
    {
        return new UpgradeVersionState
        {
            Id = source.Id,
            TenantId = source.TenantId,
            AppVersion = source.AppVersion,
            DbVersion = source.DbVersion,
            MinSupportVersion = source.MinSupportVersion,
            IsUpgrading = source.IsUpgrading,
            UpgradeNode = source.UpgradeNode,
            UpgradeStartTime = source.UpgradeStartTime
        };
    }

    /// <summary>
    /// 克隆迁移历史
    /// </summary>
    /// <param name="source">源历史</param>
    /// <returns>克隆结果</returns>
    private static UpgradeMigrationHistory CloneHistory(UpgradeMigrationHistory source)
    {
        return new UpgradeMigrationHistory
        {
            TenantId = source.TenantId,
            Version = source.Version,
            ScriptName = source.ScriptName,
            ExecutedTime = source.ExecutedTime,
            Success = source.Success,
            NodeName = source.NodeName,
            ErrorMessage = source.ErrorMessage
        };
    }

    /// <summary>
    /// 规范化版本值
    /// </summary>
    /// <param name="version">版本值</param>
    /// <returns>规范化后的版本</returns>
    private static string NormalizeVersion(string? version)
    {
        return string.IsNullOrWhiteSpace(version) ? "0.0.0" : version.Trim();
    }
}
