#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UpgradeEngine
// Guid:19c3a7c2-3c24-4d17-8a3a-1b33dc5f0e19
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 16:29:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.Application;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Upgrade.Abstractions;
using XiHan.Framework.Upgrade.Enums;
using XiHan.Framework.Upgrade.Models;
using XiHan.Framework.Upgrade.Options;
using XiHan.Framework.Upgrade.Utils;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Upgrade.Services;

/// <summary>
/// 升级引擎
/// </summary>
public class UpgradeEngine : IUpgradeEngine
{
    private readonly IUpgradeVersionStore _versionStore;
    private readonly IEnumerable<IUpgradeScriptProvider> _scriptProviders;
    private readonly IUpgradeLockProvider _lockProvider;
    private readonly IUpgradeMaintenanceModeManager _maintenanceModeManager;
    private readonly IUpgradeFileUpdater _fileUpdater;
    private readonly IRollingRestartCoordinator _restartCoordinator;
    private readonly IUpgradeTenantProvider _tenantProvider;
    private readonly IUpgradeMigrationExecutor _migrationExecutor;
    private readonly ICurrentTenant _currentTenant;
    private readonly IApplicationInfoAccessor _applicationInfo;
    private readonly XiHanUpgradeOptions _options;
    private readonly ILogger<UpgradeEngine> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="versionStore">版本存储</param>
    /// <param name="scriptProviders">脚本提供者集合</param>
    /// <param name="lockProvider">锁提供者</param>
    /// <param name="maintenanceModeManager">维护模式管理器</param>
    /// <param name="fileUpdater">文件更新器</param>
    /// <param name="restartCoordinator">重启协调器</param>
    /// <param name="tenantProvider">租户提供者</param>
    /// <param name="migrationExecutor">迁移执行器</param>
    /// <param name="currentTenant">当前租户</param>
    /// <param name="applicationInfo">应用程序信息访问器</param>
    /// <param name="options">升级选项</param>
    /// <param name="logger">日志记录器</param>
    public UpgradeEngine(
        IUpgradeVersionStore versionStore,
        IEnumerable<IUpgradeScriptProvider> scriptProviders,
        IUpgradeLockProvider lockProvider,
        IUpgradeMaintenanceModeManager maintenanceModeManager,
        IUpgradeFileUpdater fileUpdater,
        IRollingRestartCoordinator restartCoordinator,
        IUpgradeTenantProvider tenantProvider,
        IUpgradeMigrationExecutor migrationExecutor,
        ICurrentTenant currentTenant,
        IApplicationInfoAccessor applicationInfo,
        IOptions<XiHanUpgradeOptions> options,
        ILogger<UpgradeEngine> logger)
    {
        _versionStore = versionStore;
        _scriptProviders = scriptProviders;
        _lockProvider = lockProvider;
        _maintenanceModeManager = maintenanceModeManager;
        _fileUpdater = fileUpdater;
        _restartCoordinator = restartCoordinator;
        _tenantProvider = tenantProvider;
        _migrationExecutor = migrationExecutor;
        _currentTenant = currentTenant;
        _applicationInfo = applicationInfo;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 执行升级流程
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>升级开始结果</returns>
    public async Task<UpgradeStartResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var currentAppVersion = ResolveCurrentAppVersion();
        var nodeName = ResolveNodeName();

        if (!IsPrimaryNode(nodeName))
        {
            return new UpgradeStartResult
            {
                Started = false,
                Status = UpgradeStatus.Normal,
                Message = "当前节点非主节点，等待升级"
            };
        }

        if (_options.EnableMultiTenantIsolation)
        {
            foreach (var tenant in _tenantProvider.GetTenants())
            {
                using var tenantScope = _currentTenant.Change(tenant.TenantId, tenant.Name);
                var result = await ExecuteForTenantAsync(currentAppVersion, nodeName, cancellationToken);
                if (result.Status == UpgradeStatus.Failed)
                {
                    return result;
                }
            }

            return new UpgradeStartResult
            {
                Started = true,
                Status = UpgradeStatus.Completed,
                Message = "多租户升级完成"
            };
        }

        return await ExecuteForTenantAsync(currentAppVersion, nodeName, cancellationToken);
    }

    /// <summary>
    /// 获取最新的脚本版本
    /// </summary>
    /// <param name="scripts">脚本列表</param>
    /// <returns>最新的脚本版本</returns>
    private static string GetLatestScriptVersion(IReadOnlyList<UpgradeScript> scripts)
    {
        if (scripts.Count == 0)
        {
            return "0.0.0";
        }

        return scripts
            .Select(s => s.Version)
            .OrderByDescending(v => v, new SemanticVersionComparer())
            .First();
    }

    /// <summary>
    /// 针对单个租户执行升级流程
    /// </summary>
    /// <param name="currentAppVersion">当前应用版本</param>
    /// <param name="nodeName">节点名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>升级开始结果</returns>
    private async Task<UpgradeStartResult> ExecuteForTenantAsync(string currentAppVersion, string nodeName, CancellationToken cancellationToken)
    {
        await _versionStore.EnsureTablesAsync(cancellationToken);

        var version = await _versionStore.GetOrCreateAsync(currentAppVersion, _options.MinSupportVersion, cancellationToken);
        var scripts = await CollectScriptsAsync(cancellationToken);
        var latestScriptVersion = GetLatestScriptVersion(scripts);

        var needDbUpgrade = SemanticVersion.Compare(version.DbVersion, latestScriptVersion) < 0;
        var needAppUpgrade = SemanticVersion.Compare(version.AppVersion, currentAppVersion) < 0;
        var needUpgrade = needDbUpgrade || needAppUpgrade;

        _logger.LogInformation("升级检测: App {CurrentAppVersion}/{RecordedAppVersion}, Db {CurrentDbVersion}/{LatestDbVersion}, NeedUpgrade={NeedUpgrade}",
            currentAppVersion,
            version.AppVersion,
            version.DbVersion,
            latestScriptVersion,
            needUpgrade);

        if (!needUpgrade)
        {
            return new UpgradeStartResult
            {
                Started = false,
                Status = UpgradeStatus.Normal,
                Message = "无需升级"
            };
        }

        var resourceKey = BuildResourceKey(version.TenantId);
        var lockToken = await _lockProvider.TryAcquireLockAsync(resourceKey, TimeSpan.FromSeconds(_options.LockExpirySeconds), nodeName, cancellationToken);
        if (lockToken == null)
        {
            return new UpgradeStartResult
            {
                Started = false,
                Status = UpgradeStatus.Upgrading,
                Message = "升级锁已被占用"
            };
        }

        await using (lockToken)
        {
            _logger.LogInformation("已获取升级锁: {ResourceKey}", resourceKey);
            await _versionStore.SetUpgradingAsync(version, nodeName, DateTime.UtcNow, cancellationToken);

            try
            {
                if (_options.EnableMaintenanceMode)
                {
                    _logger.LogInformation("进入维护模式");
                    await _maintenanceModeManager.EnterAsync(cancellationToken);
                }

                if (scripts.Count > 0)
                {
                    _logger.LogInformation("执行数据库迁移");
                    await ExecuteMigrationsAsync(scripts, version, nodeName, cancellationToken);
                }

                _logger.LogInformation("更新系统版本信息");
                await _versionStore.SetUpgradeCompletedAsync(version, currentAppVersion, version.DbVersion, cancellationToken);

                if (_options.EnableFileUpdate)
                {
                    _logger.LogInformation("替换程序文件");
                    await _fileUpdater.ApplyAsync(cancellationToken);
                }

                if (_options.EnableMaintenanceMode)
                {
                    _logger.LogInformation("退出维护模式");
                    await _maintenanceModeManager.ExitAsync(cancellationToken);
                }

                _logger.LogInformation("释放升级锁");
                await lockToken.ReleaseAsync();

                if (_options.EnableRollingRestart)
                {
                    _logger.LogInformation("执行滚动重启");
                    await _restartCoordinator.RestartAsync(cancellationToken);
                }

                return new UpgradeStartResult
                {
                    Started = true,
                    Status = UpgradeStatus.Completed,
                    Message = "升级完成"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "升级执行失败: {Error}", ex.Message);
                await _versionStore.SetUpgradeFailedAsync(version, cancellationToken);

                if (_options.EnableMaintenanceMode)
                {
                    await _maintenanceModeManager.ExitAsync(cancellationToken);
                }

                await lockToken.ReleaseAsync();

                return new UpgradeStartResult
                {
                    Started = false,
                    Status = UpgradeStatus.Failed,
                    Message = $"升级失败: {ex.Message}"
                };
            }
        }
    }

    /// <summary>
    /// 执行数据库迁移脚本
    /// </summary>
    /// <param name="scripts">脚本列表</param>
    /// <param name="version">升级版本状态</param>
    /// <param name="nodeName">节点名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    private async Task ExecuteMigrationsAsync(
        IReadOnlyList<UpgradeScript> scripts,
        UpgradeVersionState version,
        string nodeName,
        CancellationToken cancellationToken)
    {
        var currentDbVersion = version.DbVersion;

        var groupedScripts = scripts
            .Where(s => SemanticVersion.Compare(s.Version, currentDbVersion) > 0)
            .GroupBy(s => s.Version)
            .OrderBy(g => g.Key, new SemanticVersionComparer());

        foreach (var group in groupedScripts)
        {
            foreach (var script in group.OrderBy(s => s.ScriptName, StringComparer.OrdinalIgnoreCase))
            {
                if (await _versionStore.HasMigrationHistoryAsync(script.Version, script.ScriptName, cancellationToken))
                {
                    continue;
                }

                _logger.LogInformation("执行迁移脚本: {Version}/{ScriptName}", script.Version, script.ScriptName);
                var sql = await File.ReadAllTextAsync(script.ScriptPath, cancellationToken);
                var history = new UpgradeMigrationHistory
                {
                    TenantId = version.TenantId,
                    Version = script.Version,
                    ScriptName = script.ScriptName,
                    ExecutedTime = DateTime.UtcNow,
                    Success = false,
                    NodeName = nodeName
                };

                try
                {
                    await _migrationExecutor.ExecuteAsync(sql, cancellationToken);

                    history.Success = true;
                    await _versionStore.AddMigrationHistoryAsync(history, cancellationToken);
                }
                catch (Exception ex)
                {
                    history.ErrorMessage = ex.Message;
                    await _versionStore.AddMigrationHistoryAsync(history, cancellationToken);
                    _logger.LogError(ex, "迁移脚本执行失败: {Version}/{ScriptName}", script.Version, script.ScriptName);
                    throw;
                }
            }

            currentDbVersion = group.Key;
            await _versionStore.UpdateDbVersionAsync(version, currentDbVersion, cancellationToken);
        }
    }

    /// <summary>
    /// 收集所有升级脚本
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>升级脚本列表</returns>
    private async Task<IReadOnlyList<UpgradeScript>> CollectScriptsAsync(CancellationToken cancellationToken)
    {
        var scripts = new List<UpgradeScript>();
        foreach (var provider in _scriptProviders)
        {
            var providerScripts = await provider.GetScriptsAsync(cancellationToken);
            if (providerScripts.Count > 0)
            {
                scripts.AddRange(providerScripts);
            }
        }

        return scripts;
    }

    /// <summary>
    /// 解析当前应用版本
    /// </summary>
    /// <returns>当前应用版本</returns>
    private string ResolveCurrentAppVersion()
    {
        if (!string.IsNullOrWhiteSpace(_options.AppVersion))
        {
            return _options.AppVersion;
        }

        var version = ReflectionHelper.GetEntryAssemblyVersion();
        return version?.ToString(3) ?? "0.0.0";
    }

    /// <summary>
    /// 解析节点名称
    /// </summary>
    /// <returns>节点名称</returns>
    private string ResolveNodeName()
    {
        if (!string.IsNullOrWhiteSpace(_options.NodeName))
        {
            return _options.NodeName;
        }

        var instanceId = _applicationInfo.InstanceId;
        return $"{Environment.MachineName}-{instanceId}";
    }

    /// <summary>
    /// 判断当前节点是否为主节点
    /// </summary>
    /// <param name="nodeName">节点名称</param>
    /// <returns>是否为主节点</returns>
    private bool IsPrimaryNode(string nodeName)
    {
        if (string.IsNullOrWhiteSpace(_options.PrimaryNodeName))
        {
            return true;
        }

        return string.Equals(_options.PrimaryNodeName, nodeName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 构建升级锁资源键
    /// </summary>
    /// <param name="tenantId">租户ID</param>
    /// <returns>升级锁资源键</returns>
    private string BuildResourceKey(long? tenantId)
    {
        if (!tenantId.HasValue)
        {
            return _options.LockResourceKey;
        }

        return $"{_options.LockResourceKey}:Tenant_{tenantId.Value}";
    }

    /// <summary>
    /// 语义化版本比较器
    /// </summary>
    private sealed class SemanticVersionComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            return SemanticVersion.Compare(x, y);
        }
    }
}
