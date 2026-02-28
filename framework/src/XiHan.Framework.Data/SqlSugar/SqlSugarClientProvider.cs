#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarClientProvider
// Guid:5f0a9c7b-2e1f-4a9e-9f60-9fb8e682d38a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/28 12:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Data.SqlSugar;

/// <summary>
/// SqlSugar 客户端提供器
/// </summary>
public sealed class SqlSugarClientProvider : ISqlSugarClientProvider
{
    private readonly SqlSugarScope _sqlSugarScope;
    private readonly XiHanSqlSugarCoreOptions _options;
    private readonly ICurrentTenant _currentTenant;
    private readonly HashSet<string> _configIds;

    /// <summary>
    /// 构造函数
    /// </summary>
    public SqlSugarClientProvider(
        SqlSugarScope sqlSugarScope,
        IOptions<XiHanSqlSugarCoreOptions> options,
        ICurrentTenant currentTenant)
    {
        _sqlSugarScope = sqlSugarScope;
        _options = options.Value;
        _currentTenant = currentTenant;
        _configIds = _options.ConnectionConfigs
            .Select(x => x.ConfigId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public ISqlSugarClient GetClient()
    {
        var tenantConfigId = GetCurrentTenantConfigId();
        if (tenantConfigId is null)
        {
            return _sqlSugarScope;
        }

        return _sqlSugarScope.GetConnectionScope(tenantConfigId);
    }

    /// <inheritdoc />
    public ISqlSugarClient GetClient(string? connectionConfigId)
    {
        if (string.IsNullOrWhiteSpace(connectionConfigId))
        {
            return GetClient();
        }

        return _sqlSugarScope.GetConnectionScope(connectionConfigId);
    }

    /// <inheritdoc />
    public SqlSugarScope GetScope()
    {
        return _sqlSugarScope;
    }

    /// <summary>
    /// 根据当前租户 Id 解析对应的 SqlSugar ConfigId（先匹配租户 Id，再匹配 Tenant_{id}）。
    /// </summary>
    /// <returns>匹配到的 ConfigId，无匹配时返回 null</returns>
    private string? GetCurrentTenantConfigId()
    {
        if (!_currentTenant.Id.HasValue)
        {
            return null;
        }

        var tenantIdText = _currentTenant.Id.Value.ToString();
        if (_configIds.Contains(tenantIdText))
        {
            return tenantIdText;
        }

        var prefixed = $"Tenant_{tenantIdText}";
        if (_configIds.Contains(prefixed))
        {
            return prefixed;
        }

        return null;
    }
}
