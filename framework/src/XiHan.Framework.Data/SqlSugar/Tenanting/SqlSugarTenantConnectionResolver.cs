#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarTenantConnectionResolver
// Guid:76ecb2cb-f6e5-4d0a-8a2b-b2d38ec98585
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 20:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.Data.SqlSugar.Options;
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
        _configIdArray = _options.ConnectionConfigs
            .Select(x => x.ConfigId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _configIds = _configIdArray.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public string ResolveCurrentConfigId()
    {
        return ResolveConfigId(_currentTenant.Id, _currentTenant.Name);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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
