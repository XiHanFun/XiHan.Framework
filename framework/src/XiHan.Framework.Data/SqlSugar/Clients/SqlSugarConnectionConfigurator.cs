#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarConnectionConfigurator
// Guid:4f9a2c7b-3d10-4e86-9b57-0a8d1f6e5c34
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 09:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SqlSugar;
using XiHan.Framework.Data.Extensions.DependencyInjection;
using XiHan.Framework.Data.SqlSugar.Auditing;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Data.SqlSugar.Tenanting;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Data.SqlSugar.Clients;

/// <summary>
/// SqlSugar 连接配置器默认实现
/// </summary>
/// <remarks>
/// 复用 <see cref="XiHanDataServiceCollectionExtensions"/> 中的全局过滤器 / AOP 装配逻辑，
/// 保证运行时动态注册的租户连接与启动期静态连接获得完全一致的过滤器与审计行为。
/// </remarks>
public sealed class SqlSugarConnectionConfigurator : ISqlSugarConnectionConfigurator
{
    private static readonly object AddConnectionLock = new();

    private readonly XiHanSqlSugarCoreOptions _options;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SqlSugarDataExecutingHandler _dataExecutingHandler;

    /// <summary>
    /// 构造函数
    /// </summary>
    public SqlSugarConnectionConfigurator(
        IOptions<XiHanSqlSugarCoreOptions> options,
        ICurrentTenantAccessor currentTenantAccessor,
        IServiceScopeFactory scopeFactory,
        SqlSugarDataExecutingHandler dataExecutingHandler)
    {
        _options = options.Value;
        _currentTenantAccessor = currentTenantAccessor;
        _scopeFactory = scopeFactory;
        _dataExecutingHandler = dataExecutingHandler;
    }

    /// <inheritdoc />
    public void Configure(SqlSugarScopeProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        XiHanDataServiceCollectionExtensions.ApplySugarGlobalFilters(provider, _options, _currentTenantAccessor);
        XiHanDataServiceCollectionExtensions.SetSugarAop(_scopeFactory, provider, _options, _dataExecutingHandler);
    }

    /// <inheritdoc />
    public SqlSugarScopeProvider EnsureTenantConnection(ITenant tenant, SqlSugarTenantConnection descriptor)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(descriptor);

        if (string.IsNullOrWhiteSpace(descriptor.ConfigId))
        {
            throw new InvalidOperationException("租户连接描述符的 ConfigId 不能为空。");
        }

        // fail-closed：声明库隔离却缺连接串，直接失败，绝不退回平台库造成跨库串写
        if (string.IsNullOrWhiteSpace(descriptor.ConnectionString))
        {
            throw new InvalidOperationException($"租户连接 [{descriptor.ConfigId}] 的连接字符串为空，已按 fail-closed 拒绝请求。");
        }

        var configId = descriptor.ConfigId.Trim();

        // 单例 SqlSugarScope 跨请求/线程共享，首次命中需加锁幂等注册并补挂过滤器 + AOP
        if (!tenant.IsAnyConnection(configId))
        {
            lock (AddConnectionLock)
            {
                if (!tenant.IsAnyConnection(configId))
                {
                    tenant.AddConnection(BuildConnectionConfig(configId, descriptor));
                    Configure(tenant.GetConnectionScope(configId));
                }
            }
        }

        return tenant.GetConnectionScope(configId);
    }

    private ConnectionConfig BuildConnectionConfig(string configId, SqlSugarTenantConnection descriptor)
    {
        return new ConnectionConfig
        {
            ConfigId = configId,
            ConnectionString = descriptor.ConnectionString,
            DbType = descriptor.DbType,
            IsAutoCloseConnection = descriptor.IsAutoCloseConnection,
            InitKeyType = InitKeyType.Attribute,
            MoreSettings = XiHanDataServiceCollectionExtensions.BuildMoreSettings(null, _options)
        };
    }
}
