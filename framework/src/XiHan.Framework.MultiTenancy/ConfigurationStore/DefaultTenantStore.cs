#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultTenantStore
// Guid:f0afdc4c-6d9d-4f42-b85f-c2f7a3bd6e2d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 16:42:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.Core.Data;

namespace XiHan.Framework.MultiTenancy.ConfigurationStore;

/// <summary>
/// 基于配置的默认租户存储
/// </summary>
public class DefaultTenantStore : ITenantStore
{
    private readonly IOptionsMonitor<XiHanDefaultTenantStoreOptions> _optionsMonitor;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="optionsMonitor">默认租户存储选项监视器</param>
    public DefaultTenantStore(IOptionsMonitor<XiHanDefaultTenantStoreOptions> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
    }

    /// <summary>
    /// 根据租户 Id 查询租户配置
    /// </summary>
    /// <param name="id">租户 Id</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>租户配置</returns>
    public Task<TenantConfiguration?> FindAsync(long id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tenant = GetSnapshot().FirstOrDefault(item => item.Id == id);
        return Task.FromResult(tenant);
    }

    /// <summary>
    /// 根据租户名称查询租户配置
    /// </summary>
    /// <param name="name">租户名称（Name 或 NormalizedName）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>租户配置</returns>
    public Task<TenantConfiguration?> FindAsync(string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult<TenantConfiguration?>(null);
        }

        var trimmed = name.Trim();
        if (long.TryParse(trimmed, out var tenantId))
        {
            return FindAsync(tenantId, cancellationToken);
        }

        var tenant = GetSnapshot().FirstOrDefault(item =>
            string.Equals(item.Name, trimmed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.NormalizedName, trimmed, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(tenant);
    }

    /// <summary>
    /// 获取租户配置列表
    /// </summary>
    /// <param name="includeInactive">是否包含非激活租户</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>租户配置列表</returns>
    public Task<IReadOnlyList<TenantConfiguration>> GetListAsync(bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = GetSnapshot();
        IReadOnlyList<TenantConfiguration> tenants = includeInactive
            ? snapshot
            : snapshot.Where(static item => item.IsActive).ToArray();
        return Task.FromResult(tenants);
    }

    private IReadOnlyList<TenantConfiguration> GetSnapshot()
    {
        var tenants = _optionsMonitor.CurrentValue.Tenants ?? [];
        if (tenants.Length == 0)
        {
            return [];
        }

        return tenants.Select(Clone).ToArray();
    }

    private static TenantConfiguration Clone(TenantConfiguration tenant)
    {
        var name = string.IsNullOrWhiteSpace(tenant.Name) ? tenant.Id.ToString() : tenant.Name.Trim();
        var normalizedName = string.IsNullOrWhiteSpace(tenant.NormalizedName)
            ? name.ToUpperInvariant()
            : tenant.NormalizedName.Trim();

        var cloned = new TenantConfiguration(tenant.Id, name, normalizedName, tenant.EditionId)
        {
            IsActive = tenant.IsActive,
            ConnectionStrings = CloneConnectionStrings(tenant.ConnectionStrings)
        };

        return cloned;
    }

    private static ConnectionStrings? CloneConnectionStrings(ConnectionStrings? connectionStrings)
    {
        if (connectionStrings is null || connectionStrings.Count == 0)
        {
            return null;
        }

        var cloned = new ConnectionStrings();
        foreach (var item in connectionStrings)
        {
            cloned[item.Key] = item.Value;
        }

        return cloned;
    }
}
