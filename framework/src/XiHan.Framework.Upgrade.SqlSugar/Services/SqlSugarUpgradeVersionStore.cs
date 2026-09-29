// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Upgrade.Abstractions;
using XiHan.Framework.Upgrade.Models;
using XiHan.Framework.Upgrade.SqlSugar.Entities;
using XiHan.Framework.Upgrade.SqlSugar.Mapping;

namespace XiHan.Framework.Upgrade.SqlSugar.Services;

/// <summary>
/// 升级版本存储 SqlSugar 实现
/// </summary>
public class SqlSugarUpgradeVersionStore : IUpgradeVersionStore
{
    private readonly ISqlSugarClientResolver _clientResolver;
    private readonly IDistributedIdGenerator<long> _idGenerator;
    private readonly ICurrentTenant? _currentTenant;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientResolver">客户端解析器</param>
    /// <param name="idGenerator">主键生成器</param>
    /// <param name="currentTenant">当前租户（可选）</param>
    public SqlSugarUpgradeVersionStore(
        ISqlSugarClientResolver clientResolver,
        IDistributedIdGenerator<long> idGenerator,
        ICurrentTenant? currentTenant = null)
    {
        _clientResolver = clientResolver;
        _idGenerator = idGenerator;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// 确保升级相关表存在
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public Task EnsureTablesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = _clientResolver.GetClientForEntity<SysUpgradeVersion>();
        client.CodeFirst.InitTables(typeof(SysUpgradeVersion), typeof(SysUpgradeMigrationHistory));

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取或创建系统版本记录
    /// </summary>
    /// <param name="currentAppVersion">当前应用版本</param>
    /// <param name="minSupportVersion">最小支持版本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>版本状态</returns>
    public async Task<UpgradeVersionState> GetOrCreateAsync(string currentAppVersion, string minSupportVersion, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tenantId = _currentTenant?.Id;
        var tenantKey = UpgradeMapper.BuildTenantKey(tenantId);
        var client = _clientResolver.GetClientForEntity<SysUpgradeVersion>();

        var existing = await client.Queryable<SysUpgradeVersion>()
            .Where(item => item.TenantKey == tenantKey)
            .Take(1)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            return await BackfillIfNeededAsync(client, existing[0], currentAppVersion, minSupportVersion, cancellationToken);
        }

        await OnBeforeInsertAsync(tenantKey, cancellationToken);

        var entity = new SysUpgradeVersion(_idGenerator.NextId())
        {
            TenantId = tenantId,
            TenantKey = tenantKey,
            AppVersion = UpgradeMapper.NormalizeVersion(currentAppVersion),
            DbVersion = "0.0.0",
            MinSupportVersion = UpgradeMapper.NormalizeVersion(minSupportVersion),
            IsUpgrading = false
        };

        try
        {
            await client.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            List<SysUpgradeVersion> retryExisting;

            try
            {
                retryExisting = await client.Queryable<SysUpgradeVersion>()
                    .Where(item => item.TenantKey == tenantKey)
                    .Take(1)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception requeryException)
            {
                throw new AggregateException(ex, requeryException);
            }

            if (retryExisting.Count > 0)
            {
                return UpgradeMapper.ToState(retryExisting[0]);
            }

            throw;
        }

        return UpgradeMapper.ToState(entity);
    }

    /// <summary>
    /// 当前库还没有版本记录时，按给定版本登记一条
    /// </summary>
    /// <remarks>
    /// 插入因租户键唯一索引冲突失败时按租户键重查，查到即返回 false，查不到则抛出原始异常；重查本身失败时抛出同时包含插入异常与重查异常的 <see cref="AggregateException"/>。
    /// </remarks>
    /// <param name="appVersion">应用版本</param>
    /// <param name="dbVersion">数据库版本</param>
    /// <param name="minSupportVersion">最小支持版本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>新登记返回 true，已有记录返回 false（不改动既有记录）</returns>
    public async Task<bool> TryCreateBaselineAsync(string appVersion, string dbVersion, string minSupportVersion, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tenantId = _currentTenant?.Id;
        var tenantKey = UpgradeMapper.BuildTenantKey(tenantId);
        var client = _clientResolver.GetClientForEntity<SysUpgradeVersion>();

        var exists = await client.Queryable<SysUpgradeVersion>()
            .Where(item => item.TenantKey == tenantKey)
            .AnyAsync();

        if (exists)
        {
            return false;
        }

        await OnBeforeInsertAsync(tenantKey, cancellationToken);

        var entity = new SysUpgradeVersion(_idGenerator.NextId())
        {
            TenantId = tenantId,
            TenantKey = tenantKey,
            AppVersion = UpgradeMapper.NormalizeVersion(appVersion),
            DbVersion = UpgradeMapper.NormalizeVersion(dbVersion),
            MinSupportVersion = UpgradeMapper.NormalizeVersion(minSupportVersion),
            IsUpgrading = false
        };

        try
        {
            await client.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            bool existsAfterConflict;

            try
            {
                existsAfterConflict = await client.Queryable<SysUpgradeVersion>()
                    .Where(item => item.TenantKey == tenantKey)
                    .AnyAsync();
            }
            catch (Exception requeryException)
            {
                throw new AggregateException(ex, requeryException);
            }

            if (existsAfterConflict)
            {
                return false;
            }

            throw;
        }

        return true;
    }

    /// <summary>
    /// 在确认租户键不存在、正式插入新行之前调用的钩子
    /// </summary>
    /// <remarks>
    /// 生产环境中是空操作。测试可重写它，在查询与插入之间插入一行竞争数据。
    /// </remarks>
    /// <param name="tenantKey">即将插入的租户键</param>
    /// <param name="cancellationToken">取消令牌</param>
    protected virtual Task OnBeforeInsertAsync(string tenantKey, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取最新迁移历史记录
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>迁移历史</returns>
    public async Task<UpgradeMigrationHistory?> GetLatestHistoryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tenantKey = UpgradeMapper.BuildTenantKey(_currentTenant?.Id);
        var client = _clientResolver.GetClientForEntity<SysUpgradeMigrationHistory>();

        var latest = await client.Queryable<SysUpgradeMigrationHistory>()
            .Where(item => item.TenantKey == tenantKey)
            .OrderBy(item => item.ExecutedTime, OrderByType.Desc)
            .Take(1)
            .ToListAsync(cancellationToken);

        return latest.Count > 0 ? UpgradeMapper.ToHistory(latest[0]) : null;
    }

    /// <summary>
    /// 追加迁移历史记录
    /// </summary>
    /// <param name="history">迁移历史</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task AddMigrationHistoryAsync(UpgradeMigrationHistory history, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(history);

        var tenantId = history.TenantId ?? _currentTenant?.Id;
        var tenantKey = UpgradeMapper.BuildTenantKey(tenantId);
        var client = _clientResolver.GetClientForEntity<SysUpgradeMigrationHistory>();

        var entity = new SysUpgradeMigrationHistory(_idGenerator.NextId())
        {
            TenantId = tenantId,
            TenantKey = tenantKey,
            Version = UpgradeMapper.NormalizeVersion(history.Version),
            ScriptName = history.ScriptName,
            ExecutedTime = history.ExecutedTime,
            Success = history.Success,
            NodeName = history.NodeName,
            ErrorMessage = history.ErrorMessage
        };

        await client.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    /// <summary>
    /// 是否已执行指定脚本（仅成功执行视为已执行）
    /// </summary>
    /// <param name="version">版本</param>
    /// <param name="scriptName">脚本名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否已执行</returns>
    public async Task<bool> HasMigrationHistoryAsync(string version, string scriptName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(scriptName))
        {
            return false;
        }

        var tenantKey = UpgradeMapper.BuildTenantKey(_currentTenant?.Id);
        var normalizedVersion = UpgradeMapper.NormalizeVersion(version);
        var client = _clientResolver.GetClientForEntity<SysUpgradeMigrationHistory>();

        var matched = await client.Queryable<SysUpgradeMigrationHistory>()
            .Where(item => item.TenantKey == tenantKey
                && item.Success
                && item.Version == normalizedVersion
                && item.ScriptName == scriptName)
            .Take(1)
            .ToListAsync(cancellationToken);

        return matched.Count > 0;
    }

    /// <summary>
    /// 设置升级中状态
    /// </summary>
    /// <param name="version">版本状态</param>
    /// <param name="nodeName">升级节点</param>
    /// <param name="startTime">开始时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task SetUpgradingAsync(UpgradeVersionState version, string nodeName, DateTimeOffset startTime, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsurePersisted(version);

        var client = _clientResolver.GetClientForEntity<SysUpgradeVersion>();

        await client.Updateable<SysUpgradeVersion>()
            .SetColumns(item => new SysUpgradeVersion { IsUpgrading = true, UpgradeNode = nodeName, UpgradeStartTime = startTime })
            .Where(item => item.BasicId == version.Id)
            .ExecuteCommandAsync(cancellationToken);

        version.IsUpgrading = true;
        version.UpgradeNode = nodeName;
        version.UpgradeStartTime = startTime;
    }

    /// <summary>
    /// 设置升级完成状态
    /// </summary>
    /// <param name="version">版本状态</param>
    /// <param name="appVersion">应用版本</param>
    /// <param name="dbVersion">数据库版本</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task SetUpgradeCompletedAsync(UpgradeVersionState version, string appVersion, string dbVersion, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsurePersisted(version);

        var normalizedAppVersion = UpgradeMapper.NormalizeVersion(appVersion);
        var normalizedDbVersion = UpgradeMapper.NormalizeVersion(dbVersion);
        var client = _clientResolver.GetClientForEntity<SysUpgradeVersion>();

        await client.Updateable<SysUpgradeVersion>()
            .SetColumns(item => new SysUpgradeVersion { IsUpgrading = false, AppVersion = normalizedAppVersion, DbVersion = normalizedDbVersion })
            .Where(item => item.BasicId == version.Id)
            .ExecuteCommandAsync(cancellationToken);

        version.IsUpgrading = false;
        version.AppVersion = normalizedAppVersion;
        version.DbVersion = normalizedDbVersion;
    }

    /// <summary>
    /// 设置升级失败状态
    /// </summary>
    /// <param name="version">版本状态</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task SetUpgradeFailedAsync(UpgradeVersionState version, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsurePersisted(version);

        var client = _clientResolver.GetClientForEntity<SysUpgradeVersion>();

        await client.Updateable<SysUpgradeVersion>()
            .SetColumns(item => new SysUpgradeVersion { IsUpgrading = false })
            .Where(item => item.BasicId == version.Id)
            .ExecuteCommandAsync(cancellationToken);

        version.IsUpgrading = false;
    }

    /// <summary>
    /// 更新数据库版本
    /// </summary>
    /// <param name="version">版本状态</param>
    /// <param name="dbVersion">数据库版本</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task UpdateDbVersionAsync(UpgradeVersionState version, string dbVersion, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsurePersisted(version);

        var normalizedDbVersion = UpgradeMapper.NormalizeVersion(dbVersion);
        var client = _clientResolver.GetClientForEntity<SysUpgradeVersion>();

        await client.Updateable<SysUpgradeVersion>()
            .SetColumns(item => new SysUpgradeVersion { DbVersion = normalizedDbVersion })
            .Where(item => item.BasicId == version.Id)
            .ExecuteCommandAsync(cancellationToken);

        version.DbVersion = normalizedDbVersion;
    }

    /// <summary>
    /// 回填空白的应用版本与最小支持版本
    /// </summary>
    /// <param name="client">客户端</param>
    /// <param name="entity">已存在的实体</param>
    /// <param name="currentAppVersion">当前应用版本</param>
    /// <param name="minSupportVersion">最小支持版本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>映射后的版本状态</returns>
    private static async Task<UpgradeVersionState> BackfillIfNeededAsync(
        ISqlSugarClient client,
        SysUpgradeVersion entity,
        string currentAppVersion,
        string minSupportVersion,
        CancellationToken cancellationToken)
    {
        var needsUpdate = false;

        if (string.IsNullOrWhiteSpace(entity.AppVersion))
        {
            entity.AppVersion = UpgradeMapper.NormalizeVersion(currentAppVersion);
            needsUpdate = true;
        }

        if (string.IsNullOrWhiteSpace(entity.MinSupportVersion))
        {
            entity.MinSupportVersion = UpgradeMapper.NormalizeVersion(minSupportVersion);
            needsUpdate = true;
        }

        if (needsUpdate)
        {
            await client.Updateable<SysUpgradeVersion>()
                .SetColumns(item => new SysUpgradeVersion { AppVersion = entity.AppVersion, MinSupportVersion = entity.MinSupportVersion })
                .Where(item => item.BasicId == entity.BasicId)
                .ExecuteCommandAsync(cancellationToken);
        }

        return UpgradeMapper.ToState(entity);
    }

    /// <summary>
    /// 校验版本状态是否来自 GetOrCreateAsync 的返回值
    /// </summary>
    /// <param name="version">版本状态</param>
    /// <exception cref="ArgumentNullException">version 为 null</exception>
    /// <exception cref="ArgumentException">version.Id 不是合法的已持久化标识</exception>
    private static void EnsurePersisted(UpgradeVersionState version)
    {
        ArgumentNullException.ThrowIfNull(version);

        if (version.Id <= 0)
        {
            throw new ArgumentException("version.Id 必须来自 GetOrCreateAsync 的返回值。", nameof(version));
        }
    }
}
