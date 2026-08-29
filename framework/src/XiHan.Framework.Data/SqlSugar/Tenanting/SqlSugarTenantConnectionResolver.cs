// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Data.SqlSugar.Routing;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Data.SqlSugar.Tenanting;

/// <summary>
/// SqlSugar 租户连接解析器
/// </summary>
public sealed class SqlSugarTenantConnectionResolver : ISqlSugarTenantConnectionResolver
{
    private readonly XiHanSqlSugarCoreOptions _options;
    private readonly ICurrentTenant _currentTenant;
    private readonly HashSet<string> _configIds;
    private readonly string[] _configIdArray;
    private readonly string[] _allConfigIdArray;
    private readonly string[] _moduleDataSourceNameArray;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options"></param>
    /// <param name="currentTenant"></param>
    public SqlSugarTenantConnectionResolver(
        IOptions<XiHanSqlSugarCoreOptions> options,
        ICurrentTenant currentTenant)
    {
        _options = options.Value;
        _currentTenant = currentTenant;
        _configIdArray = [.. _options.ConnectionConfigs
            .Select(x => x.ConfigId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        _configIds = _configIdArray.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 模块库不参与租户解析（上面的集合），但要参与「遍历所有库」——建表初始化与种子靠它找到模块库
        var declaredModuleConfigs = _options.ConnectionConfigs
            .Where(connectionConfig => !string.IsNullOrWhiteSpace(connectionConfig.ConfigId) &&
                                       connectionConfig.ModuleDataSourceConfigs is { Count: > 0 })
            .SelectMany(connectionConfig => connectionConfig.ModuleDataSourceConfigs!
                .Where(moduleConfig => !string.IsNullOrWhiteSpace(moduleConfig.ModuleDataSource))
                .Select(moduleConfig => (connectionConfig.ConfigId, moduleConfig.ModuleDataSource)))
            .ToArray();

        _allConfigIdArray = [.. _configIdArray
            .Concat(declaredModuleConfigs.Select(pair => ModuleDataSourceConfigIds.Build(pair.ConfigId, pair.ModuleDataSource)))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        _moduleDataSourceNameArray = [.. declaredModuleConfigs
            .Select(pair => pair.ModuleDataSource.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// 解析当前租户的连接配置标识
    /// </summary>
    /// <returns>连接配置标识</returns>
    public string ResolveCurrentConfigId()
    {
        return ResolveConfigId(_currentTenant.Id, _currentTenant.Name);
    }

    /// <summary>
    /// 根据租户标识解析连接配置标识，依次尝试自定义解析、租户Id、带前缀的租户Id、租户名称，都不命中则回退默认连接
    /// </summary>
    /// <param name="tenantId">租户Id</param>
    /// <param name="tenantName">租户名称</param>
    /// <returns>连接配置标识</returns>
    public string ResolveConfigId(long? tenantId, string? tenantName = null)
    {
        // 优先走业务自定义解析（若配置）
        var customConfigId = _options.ResolveConnectionConfigId?.Invoke(tenantId, tenantName);
        if (TryResolveConfigId(customConfigId, out var resolvedCustomConfigId))
        {
            return resolvedCustomConfigId;
        }

        if (tenantId.HasValue)
        {
            var tenantIdConfigId = tenantId.Value.ToString();
            if (TryResolveConfigId(tenantIdConfigId, out var resolvedTenantIdConfigId))
            {
                return resolvedTenantIdConfigId;
            }

            var prefixedTenantConfigId = $"{_options.TenantConfigIdPrefix}{tenantId.Value}";
            if (TryResolveConfigId(prefixedTenantConfigId, out var resolvedPrefixedTenantConfigId))
            {
                return resolvedPrefixedTenantConfigId;
            }
        }

        if (TryResolveConfigId(tenantName, out var resolvedTenantNameConfigId))
        {
            return resolvedTenantNameConfigId;
        }

        if (tenantId.HasValue && _options.ThrowIfTenantConnectionNotFound)
        {
            throw new InvalidOperationException($"未找到租户 {tenantId.Value} 对应的数据库连接配置。");
        }

        return ResolveDefaultConfigId();
    }

    /// <summary>
    /// 获取全部连接配置标识，含各连接下派生出的模块库
    /// </summary>
    /// <remarks>
    /// 供建表初始化与种子遍历所有库使用；租户解析只认顶层连接标识，不会命中模块库。
    /// </remarks>
    /// <returns>连接配置标识集合</returns>
    public IReadOnlyCollection<string> GetConfigIds()
    {
        return _allConfigIdArray;
    }

    /// <summary>
    /// 获取配置中出现过的全部模块数据源名
    /// </summary>
    /// <returns>模块数据源名集合</returns>
    public IReadOnlyCollection<string> GetModuleDataSourceNames()
    {
        return _moduleDataSourceNameArray;
    }

    private string ResolveDefaultConfigId()
    {
        if (TryResolveConfigId(_options.DefaultConfigId, out var defaultConfigId))
        {
            return defaultConfigId;
        }

        if (_configIdArray.Length > 0)
        {
            return _configIdArray[0];
        }

        throw new InvalidOperationException("SqlSugar 连接配置为空，无法解析默认连接。");
    }

    private bool TryResolveConfigId(string? configId, out string resolvedConfigId)
    {
        resolvedConfigId = string.Empty;

        if (string.IsNullOrWhiteSpace(configId))
        {
            return false;
        }

        var normalizedConfigId = configId.Trim();
        if (!_configIds.Contains(normalizedConfigId))
        {
            return false;
        }

        resolvedConfigId = normalizedConfigId;
        return true;
    }
}
