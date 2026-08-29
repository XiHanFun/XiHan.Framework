// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Initializers;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Data.SqlSugar.Routing;
using XiHan.Framework.Data.SqlSugar.Tenanting;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Data.Tests;

/// <summary>
/// 实体数据源（模块分库）与租户分库两条维度的正交性测试。
/// </summary>
/// <remarks>
/// 断言的核心契约：实体只声明「属于哪个逻辑数据源」，落到哪条连接由「数据源名 + 当前租户」共同决定；
/// 且 ConfigId 命名空间被切成互不相交的「数据源槽位」与「租户槽位」，两个解析器各取各的。
/// </remarks>
public sealed class EntityDataSourceRoutingTests
{
    private static readonly EntityDataSourceResolver Resolver = new();
    private static readonly DbInitializationContext PlatformContext = new("Default", null, isTenantDatabase: false);
    private static readonly DbInitializationContext TenantContext = new("Tenant_5", 5, isTenantDatabase: true);
    private static readonly DbInitializationContext ErpContext = new("Erp", null, isTenantDatabase: false);
    private static readonly DbInitializationContext TenantErpContext = new("Erp_Tenant_5", 5, isTenantDatabase: false);

    [Fact]
    public void 未标注数据源的实体解析为空()
    {
        Assert.Null(Resolver.ResolveDataSourceName(typeof(PlainRoutingEntity)));
    }

    [Fact]
    public void 标注数据源的实体解析出逻辑数据源名()
    {
        Assert.Equal("Erp", Resolver.ResolveDataSourceName(typeof(ErpRoutingEntity)));
    }

    [Fact]
    public void 原生租户特性同样解析为数据源()
    {
        Assert.Equal("Mes", Resolver.ResolveDataSourceName(typeof(MesRoutingEntity)));
    }

    [Fact]
    public void 派生实体继承基类的数据源()
    {
        Assert.Equal("Erp", Resolver.ResolveDataSourceName(typeof(DerivedErpRoutingEntity)));
    }

    [Fact]
    public void 注册表收齐全部被声明过的数据源名()
    {
        var registry = new DataSourceRegistry(Resolver);

        Assert.True(registry.IsDataSource("Erp"));
        Assert.True(registry.IsDataSource("eRp"));
        Assert.True(registry.IsDataSource("Mes"));
        Assert.False(registry.IsDataSource("Default"));
        Assert.False(registry.IsDataSource(null));
    }

    [Fact]
    public void 声明数据源的实体只在自己的库建表()
    {
        var provider = CreateEntityTypeProvider(new TableInitializationOptions());

        Assert.Contains(typeof(ErpRoutingEntity), provider.GetEntityTypes(ErpContext));
        Assert.DoesNotContain(typeof(ErpRoutingEntity), provider.GetEntityTypes(PlatformContext));
        Assert.DoesNotContain(typeof(ErpRoutingEntity), provider.GetEntityTypes(TenantContext));
    }

    [Fact]
    public void 声明数据源的实体也在租户级模块库建表()
    {
        var provider = CreateEntityTypeProvider(new TableInitializationOptions());

        var entityTypes = provider.GetEntityTypes(TenantErpContext);

        Assert.Contains(typeof(ErpRoutingEntity), entityTypes);
        Assert.DoesNotContain(typeof(MesRoutingEntity), entityTypes);
    }

    [Fact]
    public void 未声明数据源的实体不进模块专属库也不进租户级模块库()
    {
        var provider = CreateEntityTypeProvider(new TableInitializationOptions());

        Assert.Contains(typeof(PlainRoutingEntity), provider.GetEntityTypes(PlatformContext));
        Assert.DoesNotContain(typeof(PlainRoutingEntity), provider.GetEntityTypes(ErpContext));
        Assert.DoesNotContain(typeof(PlainRoutingEntity), provider.GetEntityTypes(TenantErpContext));
    }

    [Fact]
    public void 未声明数据源的实体照进租户独立库()
    {
        var provider = CreateEntityTypeProvider(new TableInitializationOptions());

        Assert.Contains(typeof(PlainRoutingEntity), provider.GetEntityTypes(TenantContext));
    }

    [Fact]
    public void 共享名单放行的模块库照建公共表()
    {
        var provider = CreateEntityTypeProvider(new TableInitializationOptions { SharedConnectionConfigIds = ["Erp"] });

        Assert.Contains(typeof(PlainRoutingEntity), provider.GetEntityTypes(ErpContext));
    }

    [Fact]
    public void 模块专属库判定忽略连接标识大小写()
    {
        var provider = CreateEntityTypeProvider(new TableInitializationOptions());

        var entityTypes = provider.GetEntityTypes(new DbInitializationContext("eRp", null, isTenantDatabase: false));

        Assert.Contains(typeof(ErpRoutingEntity), entityTypes);
        Assert.DoesNotContain(typeof(PlainRoutingEntity), entityTypes);
    }

    [Fact]
    public void 连接标识未知时不按数据源收窄()
    {
        var provider = CreateEntityTypeProvider(new TableInitializationOptions());

        var entityTypes = provider.GetEntityTypes(new DbInitializationContext(null, null, isTenantDatabase: false));

        Assert.Contains(typeof(ErpRoutingEntity), entityTypes);
        Assert.Contains(typeof(PlainRoutingEntity), entityTypes);
    }

    [Fact]
    public void 租户解析不会落进数据源槽位()
    {
        // 租户名称恰好等于某个数据源名：租户解析会拿名称去匹配 ConfigId，
        // 少了维度边界就会把该租户的普通实体路由进模块库
        var options = new XiHanSqlSugarCoreOptions
        {
            DefaultConfigId = "Default",
            ConnectionConfigs =
            [
                new SqlSugarConnectionConfigOptions { ConfigId = "Default" },
                new SqlSugarConnectionConfigOptions { ConfigId = "Erp" }
            ]
        };
        var resolver = new SqlSugarTenantConnectionResolver(
            Options.Create(options),
            new NoTenant(),
            new DataSourceRegistry(Resolver));

        Assert.Equal("Default", resolver.ResolveConfigId(tenantId: 7, tenantName: "Erp"));
    }

    [Fact]
    public void 租户解析不会把纯数字数据源名当作租户库()
    {
        var options = new XiHanSqlSugarCoreOptions
        {
            DefaultConfigId = "Default",
            ConnectionConfigs =
            [
                new SqlSugarConnectionConfigOptions { ConfigId = "Default" },
                new SqlSugarConnectionConfigOptions { ConfigId = "Erp" }
            ]
        };
        var resolver = new SqlSugarTenantConnectionResolver(
            Options.Create(options),
            new NoTenant(),
            new DataSourceRegistry(Resolver));

        // 租户 5 没有独立库，应回退默认连接而不是被 Erp 之类的模块库吸走
        Assert.Equal("Default", resolver.ResolveConfigId(tenantId: 5, tenantName: null));
    }

    [Fact]
    public void 命名校验放行正常的数据源名()
    {
        var validator = CreateValidator(new XiHanSqlSugarCoreOptions { DefaultConfigId = "Default" });

        validator.Validate();
    }

    [Fact]
    public void 命名校验拒绝与默认连接同名的数据源()
    {
        var validator = CreateValidator(new XiHanSqlSugarCoreOptions { DefaultConfigId = "Erp" });

        var exception = Assert.Throws<InvalidOperationException>(validator.Validate);
        Assert.Contains("DefaultConfigId", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 命名校验拒绝带租户前缀的数据源名()
    {
        var validator = CreateValidator(new XiHanSqlSugarCoreOptions
        {
            DefaultConfigId = "Default",
            TenantConfigIdPrefix = "Er"
        });

        var exception = Assert.Throws<InvalidOperationException>(validator.Validate);
        Assert.Contains("租户连接前缀", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 空实体类型解析抛出异常()
    {
        Assert.Throws<ArgumentNullException>(() => Resolver.ResolveDataSourceName(null!));
    }

    [Fact]
    public void 空数据源名的特性抛出异常()
    {
        Assert.Throws<ArgumentException>(() => new DataSourceAttribute(" "));
    }

    private static DbEntityTypeProvider CreateEntityTypeProvider(TableInitializationOptions selection)
    {
        return new DbEntityTypeProvider(
            Options.Create(new XiHanSqlSugarCoreOptions { TableInitialization = selection }),
            Resolver,
            new DataSourceRegistry(Resolver));
    }

    private static DataSourceNamingValidator CreateValidator(XiHanSqlSugarCoreOptions options)
    {
        return new DataSourceNamingValidator(new DataSourceRegistry(Resolver), Options.Create(options));
    }

    /// <summary>
    /// 未声明数据源的实体。
    /// </summary>
    [SugarTable("test_routing_plain")]
    private sealed class PlainRoutingEntity : IEntityBase
    {
        public long RowVersion { get; set; }
    }

    /// <summary>
    /// 声明落在 Erp 数据源的实体。
    /// </summary>
    [SugarTable("test_routing_erp")]
    [DataSource("Erp")]
    private class ErpRoutingEntity : IEntityBase
    {
        public long RowVersion { get; set; }
    }

    /// <summary>
    /// 继承自 Erp 数据源实体的派生实体。
    /// </summary>
    [SugarTable("test_routing_erp_derived")]
    private sealed class DerivedErpRoutingEntity : ErpRoutingEntity
    {
    }

    /// <summary>
    /// 用 SqlSugar 原生租户特性声明落在 Mes 数据源的实体。
    /// </summary>
    [SugarTable("test_routing_mes")]
    [Tenant("Mes")]
    private sealed class MesRoutingEntity : IEntityBase
    {
        public long RowVersion { get; set; }
    }

    /// <summary>
    /// 无租户上下文的当前租户实现，用于直接驱动 ResolveConfigId 的参数重载。
    /// </summary>
    private sealed class NoTenant : ICurrentTenant
    {
        public bool IsAvailable => false;

        public long? Id => null;

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
