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
/// 模块数据源（模块分库）与租户分库两条维度的正交性测试。
/// </summary>
/// <remarks>
/// 断言的核心契约：实体只声明「属于哪个模块数据源」，落到哪条连接由「模块名 + 当前布局」共同决定；
/// 模块库的 ConfigId 由父连接派生，因此同一个模块在不同布局下是不同的库，两条维度不共用命名空间。
/// </remarks>
public sealed class ModuleDataSourceRoutingTests
{
    private static readonly EntityModuleDataSourceResolver Resolver = new();
    private static readonly DbInitializationContext PlatformContext = new("Default", null, isTenantDatabase: false);
    private static readonly DbInitializationContext TenantContext = new("Tenant_5", 5, isTenantDatabase: true);
    private static readonly DbInitializationContext ErpContext = new("Default_Erp", null, isTenantDatabase: false);
    private static readonly DbInitializationContext TenantErpContext = new("Tenant_5_Erp", 5, isTenantDatabase: false);

    [Fact]
    public void 未标注模块数据源的实体解析为空()
    {
        Assert.Null(Resolver.ResolveModuleDataSource(typeof(PlainRoutingEntity)));
    }

    [Fact]
    public void 标注模块数据源的实体解析出模块名()
    {
        Assert.Equal("Erp", Resolver.ResolveModuleDataSource(typeof(ErpRoutingEntity)));
    }

    [Fact]
    public void 原生租户特性同样解析为模块数据源()
    {
        Assert.Equal("Mes", Resolver.ResolveModuleDataSource(typeof(MesRoutingEntity)));
    }

    [Fact]
    public void 派生实体继承基类的模块数据源()
    {
        Assert.Equal("Erp", Resolver.ResolveModuleDataSource(typeof(DerivedErpRoutingEntity)));
    }

    [Fact]
    public void 模块库连接标识由父连接派生()
    {
        Assert.Equal("Default_Erp", ModuleDataSourceConfigIds.Build("Default", "Erp"));
        Assert.Equal("Tenant_5_Erp", ModuleDataSourceConfigIds.Build("Tenant_5", "Erp"));
        Assert.Equal("Default_Erp", ModuleDataSourceConfigIds.Build(" Default ", " Erp "));
    }

    [Fact]
    public void 同一模块在不同布局下派生出不同连接()
    {
        Assert.NotEqual(
            ModuleDataSourceConfigIds.Build("Default", "Erp"),
            ModuleDataSourceConfigIds.Build("Tenant_5", "Erp"));
    }

    [Fact]
    public void 派生连接标识拒绝空参数()
    {
        Assert.Throws<ArgumentException>(() => ModuleDataSourceConfigIds.Build(" ", "Erp"));
        Assert.Throws<ArgumentException>(() => ModuleDataSourceConfigIds.Build("Default", " "));
    }

    [Fact]
    public void 声明模块数据源的实体只在自己的模块库建表()
    {
        var provider = CreateEntityTypeProvider(new TableInitializationOptions());

        Assert.Contains(typeof(ErpRoutingEntity), provider.GetEntityTypes(ErpContext));
        Assert.DoesNotContain(typeof(ErpRoutingEntity), provider.GetEntityTypes(PlatformContext));
        Assert.DoesNotContain(typeof(ErpRoutingEntity), provider.GetEntityTypes(TenantContext));
    }

    [Fact]
    public void 声明模块数据源的实体也在租户自带的模块库建表()
    {
        var provider = CreateEntityTypeProvider(new TableInitializationOptions());

        var entityTypes = provider.GetEntityTypes(TenantErpContext);

        Assert.Contains(typeof(ErpRoutingEntity), entityTypes);
        Assert.DoesNotContain(typeof(MesRoutingEntity), entityTypes);
    }

    [Fact]
    public void 未声明模块数据源的实体不进任何模块库()
    {
        var provider = CreateEntityTypeProvider(new TableInitializationOptions());

        Assert.Contains(typeof(PlainRoutingEntity), provider.GetEntityTypes(PlatformContext));
        Assert.DoesNotContain(typeof(PlainRoutingEntity), provider.GetEntityTypes(ErpContext));
        Assert.DoesNotContain(typeof(PlainRoutingEntity), provider.GetEntityTypes(TenantErpContext));
    }

    [Fact]
    public void 未声明模块数据源的实体照进租户独立库()
    {
        var provider = CreateEntityTypeProvider(new TableInitializationOptions());

        Assert.Contains(typeof(PlainRoutingEntity), provider.GetEntityTypes(TenantContext));
    }

    [Fact]
    public void 共享名单放行的模块库照建公共表()
    {
        var provider = CreateEntityTypeProvider(new TableInitializationOptions { SharedConnectionConfigIds = ["Default_Erp"] });

        Assert.Contains(typeof(PlainRoutingEntity), provider.GetEntityTypes(ErpContext));
    }

    [Fact]
    public void 模块库判定忽略连接标识大小写()
    {
        var provider = CreateEntityTypeProvider(new TableInitializationOptions());

        var entityTypes = provider.GetEntityTypes(new DbInitializationContext("default_eRp", null, isTenantDatabase: false));

        Assert.Contains(typeof(ErpRoutingEntity), entityTypes);
        Assert.DoesNotContain(typeof(PlainRoutingEntity), entityTypes);
    }

    [Fact]
    public void 原生租户特性声明的连接标识按相等匹配()
    {
        var provider = CreateEntityTypeProvider(new TableInitializationOptions());

        // 原生 TenantAttribute 声明的本就是连接标识本身，不走派生规则
        Assert.Contains(typeof(MesRoutingEntity), provider.GetEntityTypes(new DbInitializationContext("Mes", null, isTenantDatabase: false)));
    }

    [Fact]
    public void 连接标识未知时不按模块数据源收窄()
    {
        var provider = CreateEntityTypeProvider(new TableInitializationOptions());

        var entityTypes = provider.GetEntityTypes(new DbInitializationContext(null, null, isTenantDatabase: false));

        Assert.Contains(typeof(ErpRoutingEntity), entityTypes);
        Assert.Contains(typeof(PlainRoutingEntity), entityTypes);
    }

    [Fact]
    public void 遍历所有库时包含派生出的模块库()
    {
        // 建表初始化与种子靠这份名单找到模块库，漏了就是模块表根本不建
        var configIds = CreateTenantConnectionResolver().GetConfigIds();

        Assert.Contains("Default", configIds);
        Assert.Contains("Default_Erp", configIds);
        Assert.Contains("Default_Mes", configIds);
    }

    [Fact]
    public void 租户解析不会命中模块库()
    {
        // 租户名称恰好等于某个模块库标识：模块库不参与租户解析，应回退默认连接
        var resolver = CreateTenantConnectionResolver();

        Assert.Equal("Default", resolver.ResolveConfigId(tenantId: 7, tenantName: "Default_Erp"));
    }

    [Fact]
    public void 空实体类型解析抛出异常()
    {
        Assert.Throws<ArgumentNullException>(() => Resolver.ResolveModuleDataSource(null!));
    }

    [Fact]
    public void 空模块名的特性抛出异常()
    {
        Assert.Throws<ArgumentException>(() => new ModuleDataSourceAttribute(" "));
    }

    /// <summary>
    /// 构造建表实体提供器：模块名取自连接配置，与运行期一致
    /// </summary>
    /// <param name="selection">建表选取选项</param>
    /// <returns>建表实体提供器</returns>
    private static DbEntityTypeProvider CreateEntityTypeProvider(TableInitializationOptions selection)
    {
        return new DbEntityTypeProvider(Options.Create(CreateOptions(selection)), Resolver);
    }

    /// <summary>
    /// 构造租户连接解析器
    /// </summary>
    /// <returns>租户连接解析器</returns>
    private static SqlSugarTenantConnectionResolver CreateTenantConnectionResolver()
    {
        return new SqlSugarTenantConnectionResolver(Options.Create(CreateOptions(new TableInitializationOptions())), new NoTenant());
    }

    /// <summary>
    /// 构造一份「主库下挂两个模块库」的选项
    /// </summary>
    /// <param name="selection">建表选取选项</param>
    /// <returns>SqlSugarCore 选项</returns>
    private static XiHanSqlSugarCoreOptions CreateOptions(TableInitializationOptions selection)
    {
        return new XiHanSqlSugarCoreOptions
        {
            DefaultConfigId = "Default",
            TableInitialization = selection,
            ConnectionConfigs =
            [
                new SqlSugarConnectionConfigOptions
                {
                    ConfigId = "Default",
                    ModuleDataSourceConfigs =
                    [
                        new SqlSugarModuleDataSourceConfigOptions { ModuleDataSource = "Erp" },
                        new SqlSugarModuleDataSourceConfigOptions { ModuleDataSource = "Mes" }
                    ]
                }
            ]
        };
    }

    /// <summary>
    /// 未声明模块数据源的实体。
    /// </summary>
    [SugarTable("test_routing_plain")]
    private sealed class PlainRoutingEntity : IEntityBase
    {
        public long RowVersion { get; set; }
    }

    /// <summary>
    /// 声明落在 Erp 模块数据源的实体。
    /// </summary>
    [SugarTable("test_routing_erp")]
    [ModuleDataSource("Erp")]
    private class ErpRoutingEntity : IEntityBase
    {
        public long RowVersion { get; set; }
    }

    /// <summary>
    /// 继承自 Erp 模块数据源实体的派生实体。
    /// </summary>
    [SugarTable("test_routing_erp_derived")]
    private sealed class DerivedErpRoutingEntity : ErpRoutingEntity
    {
    }

    /// <summary>
    /// 用 SqlSugar 原生租户特性声明落在 Mes 库的实体。
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
