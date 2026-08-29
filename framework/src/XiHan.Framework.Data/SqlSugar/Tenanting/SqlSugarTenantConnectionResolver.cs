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
    private readonly IDataSourceRegistry _dataSourceRegistry;
    private readonly HashSet<string> _configIds;
    private readonly string[] _configIdArray;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options"></param>
    /// <param name="currentTenant"></param>
    /// <param name="dataSourceRegistry">数据源注册表，用于把数据源槽位排除在租户解析之外</param>
    public SqlSugarTenantConnectionResolver(
        IOptions<XiHanSqlSugarCoreOptions> options,
        ICurrentTenant currentTenant,
        IDataSourceRegistry dataSourceRegistry)
    {
        _options = options.Value;
        _currentTenant = currentTenant;
        _dataSourceRegistry = dataSourceRegistry;
        _configIdArray = [.. _options.ConnectionConfigs
            .Select(x => x.ConfigId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        _configIds = _configIdArray.ToHashSet(StringComparer.OrdinalIgnoreCase);
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
    /// 获取全部连接配置标识
    /// </summary>
    /// <returns>连接配置标识集合</returns>
    public IReadOnlyCollection<string> GetConfigIds()
    {
        return _configIdArray;
    }

    private string ResolveDefaultConfigId()
    {
        if (TryResolveConfigId(_options.DefaultConfigId, out var defaultConfigId))
        {
            return defaultConfigId;
        }

        // 兜底同样不能落进数据源槽位，否则「配错了默认连接」会静默变成「所有租户写进某个模块库」
        var firstTenantSlot = Array.Find(_configIdArray, configId => !_dataSourceRegistry.IsDataSource(configId));
        if (firstTenantSlot is not null)
        {
            return firstTenantSlot;
        }

        throw new InvalidOperationException("SqlSugar 没有可用于租户解析的连接配置（连接为空，或全部已被声明为数据源）。");
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

        // 维度边界：ConfigId 命名空间分「租户槽位」与「数据源槽位」，租户解析只在前者里挑。
        // 少了这条判断，租户 Id 或租户名恰好等于某个数据源名时（本方法会拿两者去匹配 ConfigId），
        // 该租户未声明数据源的实体会被静默路由进那个模块库。
        if (_dataSourceRegistry.IsDataSource(normalizedConfigId))
        {
            return false;
        }

        resolvedConfigId = normalizedConfigId;
        return true;
    }
}
