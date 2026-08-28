// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Initializers;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Data.SqlSugar.Routing;
using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Data.Tests;

/// <summary>
/// 实体数据源（模块分库）的解析与建表归属测试。
/// </summary>
/// <remarks>
/// 断言的是「实体声明一次数据源，仓储路由与建表初始化口径一致」这条契约：
/// 声明了数据源的实体只属于自己的库，未声明的实体不进模块专属库。
/// </remarks>
public sealed class EntityDataSourceRoutingTests
{
    private static readonly DbInitializationContext PlatformContext = new("Default", null, isTenantDatabase: false);
    private static readonly DbInitializationContext TenantContext = new("Tenant_5", 5, isTenantDatabase: true);
    private static readonly DbInitializationContext ErpContext = new("Erp", null, isTenantDatabase: false);

    [Fact]
    public void 未标注数据源的实体解析为空()
    {
        Assert.Null(new EntityDataSourceResolver().ResolveConfigId(typeof(PlainRoutingEntity)));
    }

    [Fact]
    public void 标注数据源的实体解析出连接标识()
    {
        Assert.Equal("Erp", new EntityDataSourceResolver().ResolveConfigId(typeof(ErpRoutingEntity)));
    }

    [Fact]
    public void 原生租户特性同样解析为数据源()
    {
        Assert.Equal("Mes", new EntityDataSourceResolver().ResolveConfigId(typeof(MesRoutingEntity)));
    }

    [Fact]
    public void 派生实体继承基类的数据源()
    {
        Assert.Equal("Erp", new EntityDataSourceResolver().ResolveConfigId(typeof(DerivedErpRoutingEntity)));
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
    public void 未声明数据源的实体不进模块专属库()
    {
        var provider = CreateEntityTypeProvider(new TableInitializationOptions());

        Assert.Contains(typeof(PlainRoutingEntity), provider.GetEntityTypes(PlatformContext));
        Assert.DoesNotContain(typeof(PlainRoutingEntity), provider.GetEntityTypes(ErpContext));
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
    public void 空实体类型解析抛出异常()
    {
        Assert.Throws<ArgumentNullException>(() => new EntityDataSourceResolver().ResolveConfigId(null!));
    }

    [Fact]
    public void 空连接标识的数据源特性抛出异常()
    {
        Assert.Throws<ArgumentException>(() => new DataSourceAttribute(" "));
    }

    private static DbEntityTypeProvider CreateEntityTypeProvider(TableInitializationOptions selection)
    {
        return new DbEntityTypeProvider(
            Options.Create(new XiHanSqlSugarCoreOptions { TableInitialization = selection }),
            new EntityDataSourceResolver());
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
    /// 声明落在 Erp 库的实体。
    /// </summary>
    [SugarTable("test_routing_erp")]
    [DataSource("Erp")]
    private class ErpRoutingEntity : IEntityBase
    {
        public long RowVersion { get; set; }
    }

    /// <summary>
    /// 继承自 Erp 库实体的派生实体。
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
}
