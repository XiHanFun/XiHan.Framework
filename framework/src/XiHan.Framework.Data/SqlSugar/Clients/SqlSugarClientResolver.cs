#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarClientResolver
// Guid:8d6f1c89-2e4a-4a7f-9d9b-1b5f6a8c3e2d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/17 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Tenanting;

namespace XiHan.Framework.Data.SqlSugar.Clients;

/// <summary>
/// SqlSugar 客户端解析器默认实现
/// </summary>
/// <remarks>
/// 基于 <see cref="SqlSugarScope"/>（线程安全单例）+ <see cref="ISqlSugarTenantConnectionResolver"/> 组合。
/// 当前租户上下文变化时，由 <c>ISqlSugarTenantConnectionResolver</c> 重新解析 ConfigId。
/// </remarks>
public sealed class SqlSugarClientResolver : ISqlSugarClientResolver
{
    private readonly SqlSugarScope _sqlSugarScope;
    private readonly ISqlSugarTenantConnectionResolver _tenantConnectionResolver;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="sqlSugarScope">SqlSugar 根作用域</param>
    /// <param name="tenantConnectionResolver">租户连接解析器</param>
    public SqlSugarClientResolver(
        SqlSugarScope sqlSugarScope,
        ISqlSugarTenantConnectionResolver tenantConnectionResolver)
    {
        _sqlSugarScope = sqlSugarScope;
        _tenantConnectionResolver = tenantConnectionResolver;
    }

    /// <inheritdoc />
    public ISqlSugarClient GetCurrentClient()
    {
        var configId = _tenantConnectionResolver.ResolveCurrentConfigId();
        return _sqlSugarScope.GetConnectionScope(configId);
    }

    /// <inheritdoc />
    public ISqlSugarClient GetClient(string configId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configId);
        return _sqlSugarScope.GetConnectionScope(configId.Trim());
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetAllConfigIds()
    {
        return _tenantConnectionResolver.GetConfigIds();
    }

    /// <inheritdoc />
    public IEnumerable<ISqlSugarClient> GetAllClients()
    {
        foreach (var configId in GetAllConfigIds())
        {
            yield return _sqlSugarScope.GetConnectionScope(configId);
        }
    }

    /// <inheritdoc />
    public ITenant AsTenant()
    {
        return _sqlSugarScope;
    }
}
