// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using SqlSugar;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Data.SqlSugar.Tenanting;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Data.SqlSugar.Routing;

/// <summary>
/// 数据源连接解析器默认实现
/// </summary>
/// <remarks>
/// 解析链（自上而下，命中即返回）：
/// <list type="number">
///   <item>租户上下文内且注册了 <see cref="ISqlSugarTenantDataSourceProvider"/>：由它给出该租户在此数据源上的独立库；</item>
///   <item>租户上下文内且静态配置里存在约定 ConfigId <c>{数据源名}_{租户连接前缀}{租户Id}</c>（如 <c>Erp_Tenant_1001</c>）：用它；</item>
///   <item>静态配置里存在与数据源同名的 ConfigId：用它（所有租户共享的模块库）；</item>
///   <item>都没有：fail-closed 抛异常，绝不回落主库。</item>
/// </list>
/// 第 1、2 步只在租户上下文内尝试，平台态直接走第 3 步。
/// </remarks>
public sealed class DataSourceConnectionResolver : IDataSourceConnectionResolver
{
    private readonly SqlSugarScope _sqlSugarScope;
    private readonly XiHanSqlSugarCoreOptions _options;
    private readonly ICurrentTenant _currentTenant;
    private readonly ISqlSugarConnectionConfigurator _connectionConfigurator;
    private readonly ISqlSugarTenantDataSourceProvider? _tenantDataSourceProvider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="sqlSugarScope">SqlSugar 根作用域</param>
    /// <param name="options">SqlSugarCore 选项</param>
    /// <param name="currentTenant">当前租户</param>
    /// <param name="connectionConfigurator">连接配置器</param>
    /// <param name="tenantDataSourceProviders">租户级数据源提供器（可选）</param>
    public DataSourceConnectionResolver(
        SqlSugarScope sqlSugarScope,
        IOptions<XiHanSqlSugarCoreOptions> options,
        ICurrentTenant currentTenant,
        ISqlSugarConnectionConfigurator connectionConfigurator,
        IEnumerable<ISqlSugarTenantDataSourceProvider> tenantDataSourceProviders)
    {
        _sqlSugarScope = sqlSugarScope;
        _options = options.Value;
        _currentTenant = currentTenant;
        _connectionConfigurator = connectionConfigurator;
        _tenantDataSourceProvider = tenantDataSourceProviders.FirstOrDefault();
    }

    /// <summary>
    /// 解析逻辑数据源在当前租户下对应的客户端
    /// </summary>
    /// <param name="dataSourceName">逻辑数据源名</param>
    /// <returns>该数据源对应的 Scope 级客户端</returns>
    public ISqlSugarClient ResolveClient(string dataSourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSourceName);

        var name = dataSourceName.Trim();

        if (_currentTenant.Id is { } tenantId)
        {
            // ① 业务层显式给出的租户级独立库
            var descriptor = _tenantDataSourceProvider?.Resolve(tenantId, _currentTenant.Name, name);
            if (descriptor is not null)
            {
                return _connectionConfigurator.EnsureTenantConnection(_sqlSugarScope, descriptor);
            }

            // ② 静态配置里按约定命名的租户级模块库
            var tenantScopedConfigId = BuildTenantScopedConfigId(name, tenantId);
            if (_sqlSugarScope.IsAnyConnection(tenantScopedConfigId))
            {
                return _sqlSugarScope.GetConnectionScope(tenantScopedConfigId);
            }
        }

        // ③ 所有租户共享的模块库
        if (_sqlSugarScope.IsAnyConnection(name))
        {
            return _sqlSugarScope.GetConnectionScope(name);
        }

        // ④ fail-closed：绝不回落主库，否则是静默跨库串写
        throw new XiHanException(
            $"数据源 [{name}] 没有对应的连接配置，已按 fail-closed 拒绝请求。" +
            $"请在 {XiHanSqlSugarCoreOptions.SectionName}:ConnectionConfigs 中补齐 ConfigId 为 [{name}] 的连接" +
            (_currentTenant.Id is { } id ? $"，或为租户 {id} 补齐 [{BuildTenantScopedConfigId(name, id)}]" : string.Empty) +
            "，也可注册 ISqlSugarTenantDataSourceProvider 动态提供。");
    }

    /// <summary>
    /// 拼接租户级模块库的约定 ConfigId
    /// </summary>
    /// <param name="dataSourceName">逻辑数据源名</param>
    /// <param name="tenantId">租户标识</param>
    /// <returns>约定 ConfigId，形如 <c>Erp_Tenant_1001</c></returns>
    private string BuildTenantScopedConfigId(string dataSourceName, long tenantId)
    {
        return $"{dataSourceName}_{_options.TenantConfigIdPrefix}{tenantId}";
    }
}
