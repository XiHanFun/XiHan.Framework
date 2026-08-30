// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Auditing;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Data.SqlSugar.Tenanting;
using XiHan.Framework.DistributedIds.SnowflakeIds;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Data.Tests;

/// <summary>
/// 库隔离租户建连时模块库一并注册的测试，跑在真实 SqlSugarScope 上。
/// </summary>
/// <remarks>
/// 约定本身由 <see cref="TenantModuleDatabaseConventionTests"/> 覆盖；这里守的是接线：
/// 约定算出来的模块库真的被注册进了连接容器，否则解析器按 <c>IsAnyConnection</c> 判定布局时看不见它，
/// 建库建表会漏掉这个库，运行时读写则回落公共模块库。
/// </remarks>
public sealed class TenantModuleConnectionRegistrationTests : IDisposable
{
    private readonly SqlSugarScope _scope = new(new ConnectionConfig
    {
        ConfigId = "Default",
        ConnectionString = "DataSource=platform.db;Pooling=False",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = true
    });

    [Fact]
    public void 租户建连时按约定一并注册模块库()
    {
        var configurator = CreateConfigurator(platformErpConnectionString: "DataSource=platform_erp.db;Pooling=False");

        _ = configurator.EnsureTenantConnection(_scope, TenantDescriptor("Tenant_1001", "DataSource=qqq.db;Pooling=False"));

        Assert.True(_scope.IsAnyConnection("Tenant_1001"));
        Assert.True(_scope.IsAnyConnection("Tenant_1001_Erp"));
        Assert.Contains("qqq_Erp.db", _scope.GetConnectionScope("Tenant_1001_Erp").CurrentConnectionConfig.ConnectionString, StringComparison.Ordinal);
    }

    [Fact]
    public void 平台不分库时模块连接落回租户主库()
    {
        var configurator = CreateConfigurator(platformErpConnectionString: null);

        _ = configurator.EnsureTenantConnection(_scope, TenantDescriptor("Tenant_1001", "DataSource=qqq.db;Pooling=False"));

        Assert.True(_scope.IsAnyConnection("Tenant_1001_Erp"));
        // 连接串留空即继承租户主库：模块表落进租户自己的主库，而不是公共模块库
        Assert.Contains("qqq.db", _scope.GetConnectionScope("Tenant_1001_Erp").CurrentConnectionConfig.ConnectionString, StringComparison.Ordinal);
    }

    [Fact]
    public void 关闭约定后只注册租户主库()
    {
        var configurator = CreateConfigurator(
            platformErpConnectionString: "DataSource=platform_erp.db;Pooling=False",
            enableConvention: false);

        _ = configurator.EnsureTenantConnection(_scope, TenantDescriptor("Tenant_1001", "DataSource=qqq.db;Pooling=False"));

        Assert.True(_scope.IsAnyConnection("Tenant_1001"));
        Assert.False(_scope.IsAnyConnection("Tenant_1001_Erp"));
    }

    /// <summary>
    /// 模块库派生失败时整套布局都不落地，主库不能先注册进去。
    /// </summary>
    /// <remarks>
    /// 主库一旦注册，<c>IsAnyConnection</c> 即为真，后续调用会整段跳过模块库注册——
    /// 于是第一次请求报错、第二次起「成功」但模块库永远缺席，退化成静默回落。
    /// </remarks>
    [Fact]
    public void 模块库派生失败时主库也不注册()
    {
        var configurator = CreateConfigurator(platformErpConnectionString: "DataSource=platform_erp.db;Pooling=False");
        var oracleDescriptor = new SqlSugarTenantConnection(
            "Tenant_2002",
            "Data Source=//127.0.0.1:1521/ORCL;User Id=scott;Password=tiger",
            DbType.Oracle);

        _ = Assert.Throws<NotSupportedException>(() => configurator.EnsureTenantConnection(_scope, oracleDescriptor));

        Assert.False(_scope.IsAnyConnection("Tenant_2002"));
        Assert.False(_scope.IsAnyConnection("Tenant_2002_Erp"));
    }

    /// <summary>
    /// 释放连接容器。
    /// </summary>
    public void Dispose()
    {
        _scope.Dispose();
    }

    /// <summary>
    /// 构造库隔离租户的连接描述符（不显式声明模块库，交给约定补齐）。
    /// </summary>
    private static SqlSugarTenantConnection TenantDescriptor(string configId, string connectionString)
    {
        return new SqlSugarTenantConnection(configId, connectionString, DbType.Sqlite);
    }

    /// <summary>
    /// 构造连接配置器：平台主连接下挂一个 Erp 模块库。
    /// </summary>
    /// <param name="platformErpConnectionString">平台 Erp 模块库连接串；null 表示该模块不分库</param>
    /// <param name="enableConvention">是否启用租户模块库约定</param>
    private static SqlSugarConnectionConfigurator CreateConfigurator(
        string? platformErpConnectionString,
        bool enableConvention = true)
    {
        var options = new XiHanSqlSugarCoreOptions
        {
            DefaultConfigId = "Default",
            EnableTenantModuleDatabaseConvention = enableConvention,
            ConnectionConfigs =
            [
                new SqlSugarConnectionConfigOptions
                {
                    ConfigId = "Default",
                    ConnectionString = "DataSource=platform.db;Pooling=False",
                    DbType = DbType.Sqlite,
                    ModuleDataSourceConfigs =
                    [
                        new SqlSugarModuleDataSourceConfigOptions
                        {
                            ModuleDataSource = "Erp",
                            ConnectionString = platformErpConnectionString
                        }
                    ]
                }
            ]
        };

        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        return new SqlSugarConnectionConfigurator(
            Microsoft.Extensions.Options.Options.Create(options),
            new NoTenantAccessor(),
            scopeFactory,
            new SqlSugarDataExecutingHandler(scopeFactory, new SnowflakeIdGenerator(new SnowflakeIdOptions())));
    }

    /// <summary>无租户上下文的访问器替身。</summary>
    private sealed class NoTenantAccessor : ICurrentTenantAccessor
    {
        public BasicTenantInfo? Current { get; set; }
    }
}
