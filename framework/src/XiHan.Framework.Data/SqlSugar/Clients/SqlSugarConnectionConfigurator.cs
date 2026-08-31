// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
    private static readonly Lock AddConnectionLock = new();

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

    /// <summary>
    /// 为指定连接作用域应用全局过滤器与 AOP
    /// </summary>
    /// <param name="provider">连接作用域提供器</param>
    public void Configure(SqlSugarScopeProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        XiHanDataServiceCollectionExtensions.ApplySugarGlobalFilters(provider, _options, _currentTenantAccessor);
        XiHanDataServiceCollectionExtensions.SetSugarAop(_scopeFactory, provider, _options, _dataExecutingHandler);
    }

    /// <summary>
    /// 幂等确保租户连接已注册并完成配置，返回其作用域客户端；ConfigId 或连接字符串为空时抛出异常
    /// </summary>
    /// <param name="tenant">SqlSugar 多连接容器</param>
    /// <param name="descriptor">租户连接描述符</param>
    /// <returns>该租户连接的作用域客户端</returns>
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
                    // 整套布局先算齐再落地：模块库派生失败必须抛在主库注册之前，
                    // 否则主库已在、IsAnyConnection 为真，重试会整段跳过，模块库静默缺席
                    var moduleConfigs = TenantModuleDataSourceConvention.Merge(
                        descriptor,
                        ResolveDefaultLayoutModuleConfigs(),
                        _options.EnableTenantModuleDatabaseConvention);

                    var parentConfig = BuildConnectionConfig(configId, descriptor);
                    tenant.AddConnection(parentConfig);
                    Configure(tenant.GetConnectionScope(configId));

                    // 该租户的模块库与主库一并建连：一条描述符给出的是整套布局，
                    // 分两次注册会让「主库已在、模块库还没在」的中间态被并发请求看到
                    EnsureModuleConnections(tenant, parentConfig, moduleConfigs);
                }
            }
        }

        return tenant.GetConnectionScope(configId);
    }

    /// <summary>
    /// 为租户连接注册其整套模块库
    /// </summary>
    /// <remarks>
    /// 调用方已持有注册锁。模块库的 ConfigId 由父连接派生，未填的字段继承父连接，
    /// 与静态配置里派生模块库的规则完全一致。
    /// </remarks>
    /// <param name="tenant">SqlSugar 多连接容器</param>
    /// <param name="parentConfig">已注册的父连接原生配置</param>
    /// <param name="moduleConfigs">该租户这套布局下的全部模块库配置</param>
    private void EnsureModuleConnections(ITenant tenant, ConnectionConfig parentConfig, List<SqlSugarModuleDataSourceConfigOptions> moduleConfigs)
    {
        foreach (var moduleConfig in moduleConfigs)
        {
            if (string.IsNullOrWhiteSpace(moduleConfig.ModuleDataSource))
            {
                continue;
            }

            var moduleNativeConfig = XiHanDataServiceCollectionExtensions.BuildModuleConnectionConfig(
                parentConfig, moduleConfig, _options);
            var moduleConfigId = moduleNativeConfig.ConfigId?.ToString();
            if (string.IsNullOrWhiteSpace(moduleConfigId) || tenant.IsAnyConnection(moduleConfigId))
            {
                continue;
            }

            tenant.AddConnection(moduleNativeConfig);
            Configure(tenant.GetConnectionScope(moduleConfigId));
        }
    }

    /// <summary>
    /// 取默认布局（<see cref="XiHanSqlSugarCoreOptions.DefaultConfigId"/> 那条连接）下声明的模块库
    /// </summary>
    private IEnumerable<SqlSugarModuleDataSourceConfigOptions>? ResolveDefaultLayoutModuleConfigs()
    {
        var defaultConfigId = _options.DefaultConfigId?.Trim();
        if (string.IsNullOrWhiteSpace(defaultConfigId))
        {
            return null;
        }

        return _options.ConnectionConfigs
            .Find(config => string.Equals(config.ConfigId?.Trim(), defaultConfigId, StringComparison.OrdinalIgnoreCase))
            ?.ModuleDataSourceConfigs;
    }

    private ConnectionConfig BuildConnectionConfig(string configId, SqlSugarTenantConnection descriptor)
    {
        var config = new ConnectionConfig
        {
            ConfigId = configId,
            ConnectionString = descriptor.ConnectionString,
            DbType = descriptor.DbType,
            IsAutoCloseConnection = descriptor.IsAutoCloseConnection,
            InitKeyType = InitKeyType.Attribute,
            MoreSettings = XiHanDataServiceCollectionExtensions.BuildMoreSettings(null, _options),
            // 库隔离租户同样支持读写分离，权重按框架默认归一化
            SlaveConnectionConfigs = XiHanDataServiceCollectionExtensions.NormalizeSlaveHitRates(descriptor.SlaveConnectionConfigs, _options)
        };

        // 租户连接与静态连接一致，构建前交给调用方钩子定制（以单元素列表触发，调用方按 ConfigId 分支处理）
        _options.ConfigureConnectionConfigs?.Invoke([config]);

        return config;
    }
}
