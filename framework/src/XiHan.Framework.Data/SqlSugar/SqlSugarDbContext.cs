#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarDbContext
// Guid:2c0cd8e5-f6ee-4f80-b1a6-861c633e6c7e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 20:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Tenanting;

namespace XiHan.Framework.Data.SqlSugar;

/// <summary>
/// SqlSugar 数据上下文实现
/// </summary>
public sealed class SqlSugarDbContext : ISqlSugarDbContext
{
    private readonly SqlSugarScope _sqlSugarScope;
    private readonly ISqlSugarTenantConnectionResolver _connectionResolver;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="sqlSugarScope"></param>
    /// <param name="connectionResolver"></param>
    public SqlSugarDbContext(
        SqlSugarScope sqlSugarScope,
        ISqlSugarTenantConnectionResolver connectionResolver)
    {
        _sqlSugarScope = sqlSugarScope;
        _connectionResolver = connectionResolver;
    }

    /// <inheritdoc />
    public SqlSugarScope Scope => _sqlSugarScope;

    /// <inheritdoc />
    public ISqlSugarClient GetClient()
    {
        return _sqlSugarScope.GetConnectionScope(ResolveCurrentConfigId());
    }

    /// <inheritdoc />
    public ISqlSugarClient GetClient(string? connectionConfigId)
    {
        if (string.IsNullOrWhiteSpace(connectionConfigId))
        {
            return GetClient();
        }

        return _sqlSugarScope.GetConnectionScope(connectionConfigId.Trim());
    }

    /// <inheritdoc />
    public string ResolveCurrentConfigId()
    {
        return _connectionResolver.ResolveCurrentConfigId();
    }
}
