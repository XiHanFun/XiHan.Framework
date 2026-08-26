// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Initializers;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Data.SqlSugar.Seeders;
using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Data.Tests;

/// <summary>
/// 建表与种子的选取规则测试。
/// </summary>
/// <remarks>
/// 断言的是「开发者能决定哪些表建、哪些种子跑」这条契约：特性声明、选项名单、目标库与连接范围
/// 各自独立生效，且默认配置下行为与全量初始化一致（不标注即照建照跑）。
/// </remarks>
public sealed class DbInitializationSelectionTests
{
    private static readonly DbInitializationContext PlatformContext = new("Default", null, isTenantDatabase: false);
    private static readonly DbInitializationContext TenantContext = new("Tenant_5", 5, isTenantDatabase: true);

    [Fact]
    public void 默认模式下未标注的实体照常建表()
    {
        var entityTypes = CreateEntityTypeProvider(new TableInitializationOptions()).GetEntityTypes(PlatformContext);

        Assert.Contains(typeof(PlainSelectionEntity), entityTypes);
    }

    [Fact]
    public void 标注禁用的实体不建表()
    {
        var entityTypes = CreateEntityTypeProvider(new TableInitializationOptions()).GetEntityTypes(PlatformContext);

        Assert.DoesNotContain(typeof(DisabledSelectionEntity), entityTypes);
    }

    [Fact]
    public void 按需模式下只建标注过的实体()
    {
        var provider = CreateEntityTypeProvider(new TableInitializationOptions { Mode = DbInitializationMode.OptIn });

        var entityTypes = provider.GetEntityTypes(PlatformContext);

        Assert.Contains(typeof(ReportGroupSelectionEntity), entityTypes);
        Assert.DoesNotContain(typeof(PlainSelectionEntity), entityTypes);
    }

    [Fact]
    public void 排除分组的实体不建表()
    {
        var provider = CreateEntityTypeProvider(new TableInitializationOptions { ExcludedGroups = ["Report"] });

        var entityTypes = provider.GetEntityTypes(PlatformContext);

        Assert.DoesNotContain(typeof(ReportGroupSelectionEntity), entityTypes);
        Assert.Contains(typeof(PlainSelectionEntity), entityTypes);
    }

    [Fact]
    public void 仅包含分组时未分组实体不建表()
    {
        var provider = CreateEntityTypeProvider(new TableInitializationOptions { IncludedGroups = ["Report"] });

        var entityTypes = provider.GetEntityTypes(PlatformContext);

        Assert.Contains(typeof(ReportGroupSelectionEntity), entityTypes);
        Assert.DoesNotContain(typeof(PlainSelectionEntity), entityTypes);
    }

    [Fact]
    public void 排除名单支持通配并按表名匹配()
    {
        var provider = CreateEntityTypeProvider(new TableInitializationOptions { ExcludedTables = ["test_selection_plain*"] });

        var entityTypes = provider.GetEntityTypes(PlatformContext);

        Assert.DoesNotContain(typeof(PlainSelectionEntity), entityTypes);
    }

    [Fact]
    public void 平台库实体不在租户库建表()
    {
        var provider = CreateEntityTypeProvider(new TableInitializationOptions());

        Assert.Contains(typeof(PlatformOnlySelectionEntity), provider.GetEntityTypes(PlatformContext));
        Assert.DoesNotContain(typeof(PlatformOnlySelectionEntity), provider.GetEntityTypes(TenantContext));
    }

    [Fact]
    public void 限定连接的实体只在该连接建表()
    {
        var provider = CreateEntityTypeProvider(new TableInitializationOptions());

        Assert.DoesNotContain(typeof(ArchiveConnectionSelectionEntity), provider.GetEntityTypes(PlatformContext));
        Assert.Contains(
            typeof(ArchiveConnectionSelectionEntity),
            provider.GetEntityTypes(new DbInitializationContext("Archive", null, isTenantDatabase: false)));
    }

    [Fact]
    public void 自定义委托可否决建表()
    {
        var provider = CreateEntityTypeProvider(new TableInitializationOptions
        {
            Filter = entityType => entityType != typeof(PlainSelectionEntity)
        });

        Assert.DoesNotContain(typeof(PlainSelectionEntity), provider.GetEntityTypes(PlatformContext));
    }

    [Fact]
    public void 默认模式下未标注的种子照常执行()
    {
        var seeders = CreateSeederSelector(new DataSeedingOptions())
            .Select([new PlainSeeder(), new DemoSeeder()], PlatformContext);

        Assert.Equal(2, seeders.Count);
    }

    [Fact]
    public void 排除分组的种子不执行()
    {
        var seeders = CreateSeederSelector(new DataSeedingOptions { ExcludedGroups = ["Demo"] })
            .Select([new PlainSeeder(), new DemoSeeder()], PlatformContext);

        Assert.Equal([nameof(PlainSeeder)], seeders.Select(seeder => seeder.Name));
    }

    [Fact]
    public void 派生种子继承基类分组()
    {
        var seeders = CreateSeederSelector(new DataSeedingOptions { ExcludedGroups = ["Demo"] })
            .Select([new DerivedDemoSeeder()], PlatformContext);

        Assert.Empty(seeders);
    }

    [Fact]
    public void 按需模式下只执行标注过的种子()
    {
        var seeders = CreateSeederSelector(new DataSeedingOptions { Mode = DbInitializationMode.OptIn })
            .Select([new PlainSeeder(), new DemoSeeder()], PlatformContext);

        Assert.Equal([nameof(DemoSeeder)], seeders.Select(seeder => seeder.Name));
    }

    [Fact]
    public void 排除名单按种子名称匹配()
    {
        var seeders = CreateSeederSelector(new DataSeedingOptions { ExcludedSeeders = ["Plain*"] })
            .Select([new PlainSeeder(), new DemoSeeder()], PlatformContext);

        Assert.Equal([nameof(DemoSeeder)], seeders.Select(seeder => seeder.Name));
    }

    [Fact]
    public void 自定义委托可否决种子()
    {
        var seeders = CreateSeederSelector(new DataSeedingOptions
        {
            Filter = seeder => seeder.Name != nameof(DemoSeeder)
        }).Select([new PlainSeeder(), new DemoSeeder()], PlatformContext);

        Assert.Equal([nameof(PlainSeeder)], seeders.Select(seeder => seeder.Name));
    }

    private static DbEntityTypeProvider CreateEntityTypeProvider(TableInitializationOptions selection)
    {
        return new DbEntityTypeProvider(Options.Create(new XiHanSqlSugarCoreOptions { TableInitialization = selection }));
    }

    private static DataSeederSelector CreateSeederSelector(DataSeedingOptions selection)
    {
        return new DataSeederSelector(Options.Create(new XiHanSqlSugarCoreOptions { DataSeeding = selection }));
    }

    /// <summary>
    /// 未标注选取特性的实体。
    /// </summary>
    [SugarTable("test_selection_plain")]
    private sealed class PlainSelectionEntity : IEntityBase
    {
        public long RowVersion { get; set; }
    }

    /// <summary>
    /// 显式声明不参与建表的实体。
    /// </summary>
    [SugarTable("test_selection_disabled")]
    [TableInitialization(false)]
    private sealed class DisabledSelectionEntity : IEntityBase
    {
        public long RowVersion { get; set; }
    }

    /// <summary>
    /// 归入 Report 分组的实体。
    /// </summary>
    [SugarTable("test_selection_report")]
    [TableInitialization(Group = "Report")]
    private sealed class ReportGroupSelectionEntity : IEntityBase
    {
        public long RowVersion { get; set; }
    }

    /// <summary>
    /// 只在平台库建表的实体。
    /// </summary>
    [SugarTable("test_selection_platform_only")]
    [TableInitialization(Target = DbInitializationTarget.Platform)]
    private sealed class PlatformOnlySelectionEntity : IEntityBase
    {
        public long RowVersion { get; set; }
    }

    /// <summary>
    /// 只在 Archive 连接建表的实体。
    /// </summary>
    [SugarTable("test_selection_archive")]
    [TableInitialization(ConnectionConfigIds = ["Archive"])]
    private sealed class ArchiveConnectionSelectionEntity : IEntityBase
    {
        public long RowVersion { get; set; }
    }

    /// <summary>
    /// 未标注选取特性的种子。
    /// </summary>
    private sealed class PlainSeeder : IDataSeeder
    {
        public int Order => 1;

        public string Name => nameof(PlainSeeder);

        public Task SeedAsync() => Task.CompletedTask;
    }

    /// <summary>
    /// 归入 Demo 分组的种子。
    /// </summary>
    [DataSeeding(Group = "Demo")]
    private class DemoSeeder : IDataSeeder
    {
        public int Order => 2;

        public virtual string Name => nameof(DemoSeeder);

        public Task SeedAsync() => Task.CompletedTask;
    }

    /// <summary>
    /// 继承自 Demo 分组种子的派生种子。
    /// </summary>
    private sealed class DerivedDemoSeeder : DemoSeeder
    {
        public override string Name => nameof(DerivedDemoSeeder);
    }
}
