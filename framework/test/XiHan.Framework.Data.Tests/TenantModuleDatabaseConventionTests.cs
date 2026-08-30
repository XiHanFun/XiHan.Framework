// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Data.SqlSugar.Tenanting;

namespace XiHan.Framework.Data.Tests;

/// <summary>
/// 库隔离租户「自带整套模块库」的约定测试。
/// </summary>
/// <remarks>
/// 守的是一条承诺：<b>租户声明了库隔离，它的数据就都在它自己的库里</b>。
/// 少了这条约定，主库确实独立了，但标了 <c>[ModuleDataSource]</c> 的表会回落公共模块库，
/// 只剩 <c>TenantId</c> 列做区分——隔离承诺兑现了一半，而且没有任何提示。
/// </remarks>
public sealed class TenantModuleDatabaseConventionTests
{
    /// <summary>
    /// 平台分了模块库，租户就有一个同名模块库，库名由租户主库名派生。
    /// </summary>
    [Fact]
    public void 平台分了模块库时租户镜像出自己的模块库()
    {
        var merged = TenantModuleDataSourceConvention.Merge(
            TenantDescriptor("Server=127.0.0.1;Database=qqq;Username=postgres"),
            [PlatformModule("Erp", "Server=127.0.0.1;Database=XiHanBasicAppErp;Username=postgres")],
            enabled: true);

        var erp = Assert.Single(merged);
        Assert.Equal("Erp", erp.ModuleDataSource);
        Assert.Equal("qqq_Erp", DatabaseNameOf(erp.ConnectionString));
    }

    /// <summary>
    /// 平台那条模块连接串留空表示该模块不分库，租户跟着不分：留空即继承租户主库。
    /// </summary>
    [Fact]
    public void 平台不分库的模块租户也不分库()
    {
        var merged = TenantModuleDataSourceConvention.Merge(
            TenantDescriptor("Server=127.0.0.1;Database=qqq;Username=postgres"),
            [PlatformModule("Erp", connectionString: null)],
            enabled: true);

        var erp = Assert.Single(merged);
        Assert.Equal("Erp", erp.ModuleDataSource);
        Assert.Null(erp.ConnectionString);
    }

    /// <summary>
    /// 提供器显式给出的模块库优先，约定不覆盖它。
    /// </summary>
    [Fact]
    public void 显式声明的模块库优先于约定()
    {
        var declared = new SqlSugarModuleDataSourceConfigOptions
        {
            ModuleDataSource = "Erp",
            ConnectionString = "Server=10.0.0.9;Database=ErpOnAnotherHost;Username=postgres"
        };

        var merged = TenantModuleDataSourceConvention.Merge(
            TenantDescriptor("Server=127.0.0.1;Database=qqq;Username=postgres", declared),
            [PlatformModule("Erp", "Server=127.0.0.1;Database=XiHanBasicAppErp;Username=postgres")],
            enabled: true);

        var erp = Assert.Single(merged);
        Assert.Equal("ErpOnAnotherHost", DatabaseNameOf(erp.ConnectionString));
    }

    /// <summary>
    /// 显式只说了一部分时，其余模块仍由约定补齐。
    /// </summary>
    [Fact]
    public void 约定只补显式没提到的模块()
    {
        var declared = new SqlSugarModuleDataSourceConfigOptions
        {
            ModuleDataSource = "Erp",
            ConnectionString = "Server=10.0.0.9;Database=ErpOnAnotherHost;Username=postgres"
        };

        var merged = TenantModuleDataSourceConvention.Merge(
            TenantDescriptor("Server=127.0.0.1;Database=qqq;Username=postgres", declared),
            [
                PlatformModule("Erp", "Server=127.0.0.1;Database=XiHanBasicAppErp;Username=postgres"),
                PlatformModule("Mes", "Server=127.0.0.1;Database=XiHanBasicAppMes;Username=postgres")
            ],
            enabled: true);

        Assert.Equal(["Erp", "Mes"], merged.Select(item => item.ModuleDataSource));
        Assert.Equal("ErpOnAnotherHost", DatabaseNameOf(merged[0].ConnectionString));
        Assert.Equal("qqq_Mes", DatabaseNameOf(merged[1].ConnectionString));
    }

    /// <summary>
    /// 同名判定不区分大小写，不能因为大小写不同镜像出第二条。
    /// </summary>
    [Fact]
    public void 模块重名判定不区分大小写()
    {
        var declared = new SqlSugarModuleDataSourceConfigOptions
        {
            ModuleDataSource = "erp",
            ConnectionString = "Server=10.0.0.9;Database=ErpOnAnotherHost;Username=postgres"
        };

        var merged = TenantModuleDataSourceConvention.Merge(
            TenantDescriptor("Server=127.0.0.1;Database=qqq;Username=postgres", declared),
            [PlatformModule("Erp", "Server=127.0.0.1;Database=XiHanBasicAppErp;Username=postgres")],
            enabled: true);

        _ = Assert.Single(merged);
    }

    /// <summary>
    /// 关掉约定即退回旧行为：只剩显式声明的部分，模块表回落公共模块库。
    /// </summary>
    [Fact]
    public void 关闭约定后不再镜像模块库()
    {
        var merged = TenantModuleDataSourceConvention.Merge(
            TenantDescriptor("Server=127.0.0.1;Database=qqq;Username=postgres"),
            [PlatformModule("Erp", "Server=127.0.0.1;Database=XiHanBasicAppErp;Username=postgres")],
            enabled: false);

        Assert.Empty(merged);
    }

    /// <summary>
    /// 平台压根没有模块库时，租户也不会凭空多出模块库。
    /// </summary>
    [Fact]
    public void 平台没有模块库时租户也没有()
    {
        Assert.Empty(TenantModuleDataSourceConvention.Merge(
            TenantDescriptor("Server=127.0.0.1;Database=qqq;Username=postgres"),
            defaultLayoutModuleConfigs: null,
            enabled: true));

        Assert.Empty(TenantModuleDataSourceConvention.Merge(
            TenantDescriptor("Server=127.0.0.1;Database=qqq;Username=postgres"),
            [],
            enabled: true));
    }

    /// <summary>
    /// 构造一个库隔离租户的连接描述符。
    /// </summary>
    private static SqlSugarTenantConnection TenantDescriptor(
        string connectionString,
        params SqlSugarModuleDataSourceConfigOptions[] declaredModules)
    {
        return new SqlSugarTenantConnection(
            "Tenant_1001",
            connectionString,
            DbType.PostgreSQL,
            ModuleDataSourceConfigs: declaredModules.Length == 0 ? null : [.. declaredModules]);
    }

    /// <summary>
    /// 构造一条平台主连接下的模块库配置。
    /// </summary>
    private static SqlSugarModuleDataSourceConfigOptions PlatformModule(string moduleDataSource, string? connectionString)
    {
        return new SqlSugarModuleDataSourceConfigOptions
        {
            ModuleDataSource = moduleDataSource,
            ConnectionString = connectionString
        };
    }

    /// <summary>
    /// 从连接串里取库名，断言落点而不是断言字符串拼法。
    /// </summary>
    private static string? DatabaseNameOf(string? connectionString)
    {
        var builder = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = connectionString };
        return builder.TryGetValue("Database", out var database) ? database?.ToString() : null;
    }
}
