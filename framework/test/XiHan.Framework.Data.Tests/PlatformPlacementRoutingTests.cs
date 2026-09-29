// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Data.SqlSugar.Initializers;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Data.SqlSugar.Routing;
using XiHan.Framework.Data.SqlSugar.Tenanting;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Abstracts;

namespace XiHan.Framework.Data.Tests;

/// <summary>
/// 固定落平台库的实体：运行期无论当前租户是否独立库都走平台库，建表只在平台库建。
/// </summary>
public sealed class PlatformPlacementRoutingTests : IDisposable
{
    private static readonly EntityModuleDataSourceResolver Resolver = new();
    private readonly List<string> _databasePaths = [];
    private readonly SqlSugarScope _scope;

    /// <summary>
    /// 平台库与一个租户独立库两条真实 SQLite 连接
    /// </summary>
    public PlatformPlacementRoutingTests()
    {
        _scope = new SqlSugarScope([BuildConfig("Default"), BuildConfig("Tenant_5")]);
    }

    [Fact]
    public void 标注平台库的实体解析为平台落点_未标注的不是()
    {
        Assert.True(Resolver.IsPlatformPlaced(typeof(PlatformEntity)));
        Assert.True(Resolver.IsPlatformPlaced(typeof(DerivedPlatformEntity)));
        Assert.False(Resolver.IsPlatformPlaced(typeof(TenantEntity)));
    }

    [Fact]
    public void 同时声明平台库与模块数据源是配置错误()
    {
        _ = Assert.Throws<InvalidOperationException>(() => Resolver.IsPlatformPlaced(typeof(ConflictingEntity)));
    }

    [Fact]
    public void 平台库实体只在平台库建表()
    {
        var provider = new DbEntityTypeProvider(Options.Create(CreateOptions()), Resolver);

        Assert.Contains(typeof(PlatformEntity), provider.GetEntityTypes(new DbInitializationContext("Default", null, isTenantDatabase: false)));
        Assert.DoesNotContain(typeof(PlatformEntity), provider.GetEntityTypes(new DbInitializationContext("Tenant_5", 5, isTenantDatabase: true)));
        Assert.Contains(typeof(TenantEntity), provider.GetEntityTypes(new DbInitializationContext("Tenant_5", 5, isTenantDatabase: true)));
    }

    [Fact]
    public void 独立库租户里_平台库实体仍走平台库_其余走租户库()
    {
        var resolver = CreateResolver(currentTenantId: 5);

        Assert.Equal("Tenant_5", resolver.GetClientForEntity(typeof(TenantEntity)).CurrentConnectionConfig.ConfigId);
        Assert.Equal("Default", resolver.GetClientForEntity(typeof(PlatformEntity)).CurrentConnectionConfig.ConfigId);
    }

    [Fact]
    public void 平台上下文里两类实体都在平台库()
    {
        var resolver = CreateResolver(currentTenantId: null);

        Assert.Equal("Default", resolver.GetClientForEntity(typeof(TenantEntity)).CurrentConnectionConfig.ConfigId);
        Assert.Equal("Default", resolver.GetClientForEntity(typeof(PlatformEntity)).CurrentConnectionConfig.ConfigId);
    }

    /// <summary>
    /// 释放连接与临时库文件
    /// </summary>
    public void Dispose()
    {
        _scope.Dispose();
        foreach (var path in _databasePaths.Where(File.Exists))
        {
            File.Delete(path);
        }
    }

    private static XiHanSqlSugarCoreOptions CreateOptions()
    {
        return new XiHanSqlSugarCoreOptions
        {
            DefaultConfigId = "Default",
            TableInitialization = new TableInitializationOptions(),
            ConnectionConfigs = [new SqlSugarConnectionConfigOptions { ConfigId = "Default" }]
        };
    }

    private SqlSugarClientResolver CreateResolver(long? currentTenantId)
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddLogging();
        services.AddSingleton<IAmbientUnitOfWork, AmbientUnitOfWork>();
        services.AddSingleton<IUnitOfWorkEventPublisher, NullUnitOfWorkEventPublisher>();
        services.AddTransient<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IUnitOfWorkManager, UnitOfWorkManager>();
        var serviceProvider = services.BuildServiceProvider();

        return new SqlSugarClientResolver(
            _scope,
            new DefaultResolver(),
            Resolver,
            new ThrowingModuleConnectionResolver(),
            serviceProvider.GetRequiredService<IUnitOfWorkManager>(),
            new FixedTenant(currentTenantId),
            new ExistingConnectionConfigurator(),
            [new IsolatedTenantProvider()]);
    }

    private ConnectionConfig BuildConfig(string configId)
    {
        var path = Path.Combine(Path.GetTempPath(), $"xihan-placement-{configId}-{Guid.NewGuid():N}.db");
        _databasePaths.Add(path);

        return new ConnectionConfig
        {
            ConfigId = configId,
            ConnectionString = $"DataSource={path};Pooling=False",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
        };
    }

    [SugarTable("placement_platform")]
    [PlatformDataSource]
    private class PlatformEntity : IEntityBase
    {
        public long RowVersion { get; set; }
    }

    [SugarTable("placement_platform_derived")]
    private sealed class DerivedPlatformEntity : PlatformEntity;

    [SugarTable("placement_tenant")]
    private sealed class TenantEntity : IEntityBase
    {
        public long RowVersion { get; set; }
    }

    // 不标 SugarTable：建表扫描会扫到带表特性的类型，配置错误的类型只给解析器单测用
    [PlatformDataSource]
    [ModuleDataSource("Erp")]
    private sealed class ConflictingEntity : IEntityBase
    {
        public long RowVersion { get; set; }
    }

    private sealed class DefaultResolver : ISqlSugarTenantConnectionResolver
    {
        public string ResolveCurrentConfigId() => "Default";

        public string ResolveConfigId(long? tenantId, string? tenantName = null) => "Default";

        public IReadOnlyCollection<string> GetConfigIds() => ["Default"];

        public IReadOnlyCollection<string> GetModuleDataSourceNames() => [];
    }

    /// <summary>
    /// 租户 5 是库隔离租户，独立连接 Tenant_5（用例里已在 Scope 中）
    /// </summary>
    private sealed class IsolatedTenantProvider : ISqlSugarTenantConnectionProvider
    {
        public SqlSugarTenantConnection? Resolve(long tenantId, string? tenantName) =>
            tenantId == 5 ? new SqlSugarTenantConnection("Tenant_5", "unused", DbType.Sqlite) : null;
    }

    private sealed class ExistingConnectionConfigurator : ISqlSugarConnectionConfigurator
    {
        public void Configure(SqlSugarScopeProvider provider)
        {
        }

        public SqlSugarScopeProvider EnsureTenantConnection(ITenant tenant, SqlSugarTenantConnection descriptor) =>
            tenant.GetConnectionScope(descriptor.ConfigId);
    }

    private sealed class ThrowingModuleConnectionResolver : IModuleDataSourceConnectionResolver
    {
        public ISqlSugarClient ResolveClient(string moduleDataSource, string parentConfigId) =>
            throw new InvalidOperationException("本用例不应触发模块数据源路由。");
    }

    private sealed class FixedTenant(long? id) : ICurrentTenant
    {
        public bool IsAvailable => id is > 0;

        public long? Id => id;

        public string? Name => null;

        public IDisposable Change(long? id, string? name = null) => new NoopScope();

        private sealed class NoopScope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
