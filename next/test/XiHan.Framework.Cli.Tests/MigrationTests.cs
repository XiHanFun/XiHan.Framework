// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Cli.Migration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Tests;

/// <summary>
/// migrate v4 的规则：模块→特性、实体协议、注入标记、时钟、工作单元、包引用与 using；整次运行的幂等、生成文件、改名与试运行。
/// 与 <see cref="CliHostTests"/> 同一集合，标记文案在期望与实际之间保持同一消息作用域。
/// </summary>
[Collection("Cli")]
public sealed class MigrationTests
{
    /// <summary>
    /// 对一段源码施加一条规则；换行统一成 LF。
    /// </summary>
    private static (string Text, MigrationSession Session) Apply(V4Fixer fixer, string source, string relativePath = "App/Sample.cs", string? projectDirectory = null)
    {
        var session = new MigrationSession();
        var file = new MigrationFile(Path.Combine(Path.GetTempPath(), relativePath.Replace('/', Path.DirectorySeparatorChar)), relativePath, projectDirectory, "\n");
        var text = fixer.Apply(file, Lf(source), session);
        return (Lf(text), session);
    }

    /// <summary>
    /// 统一成 LF。
    /// </summary>
    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public void ModuleToFeature_MergesHooks_MapsDependsOn_AndRenames()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            using XiHan.Framework.Core.Modularity;

            namespace XiHan.Shop.Orders;

            /// <summary>
            /// 订单模块
            /// </summary>
            [DependsOn(typeof(XiHanDataModule), typeof(XiHanCachingModule), typeof(XiHanHttpModule), typeof(XiHanShopCoreModule))]
            public class XiHanShopOrdersModule : XiHanModule
            {
                public override bool SkipAutoServiceRegistration => true;

                public override void PreConfigureServices(ServiceConfigurationContext context)
                {
                    Configure<OrderOptions>(options => options.Limit = 3);
                }

                public override void ConfigureServices(ServiceConfigurationContext context)
                {
                    var services = context.Services;
                    var configuration = services.GetConfiguration();
                    services.AddOrders(configuration);
                }

                public override void OnApplicationInitialization(ApplicationInitializationContext context)
                {
                    var app = context.GetApplicationBuilder();
                    app.UseOrders();
                    context.ServiceProvider.GetRequiredService<OrderWarmup>().Run(typeof(XiHanShopOrdersModule));
                }

                public override Task OnApplicationShutdownAsync(ApplicationShutdownContext context)
                {
                    context.ServiceProvider.GetRequiredService<OrderWarmup>().Stop();
                    return Task.CompletedTask;
                }

                private static void Helper()
                {
                }
            }

            """;
        var expected = $$"""
            using Microsoft.Extensions.DependencyInjection;
            using XiHan.Framework.Caching.Abstractions;
            using XiHan.Framework.Core.Modularity;
            using XiHan.Framework.Kernel;
            using XiHan.Framework.Kernel.Hosting;
            using XiHan.Framework.Persistence.Abstractions;
            using XiHan.Framework.Primitives;
            using XiHan.Framework.Primitives.Manifests;

            namespace XiHan.Shop.Orders;

            /// <summary>
            /// 订单模块
            /// </summary>
            {{Markers.Line("V4-01", CliMessages.MigrateMarkerModulesDropped.L(("modules", "XiHanHttpModule")))}}
            public sealed class ShopOrdersFeature : IHostedFeature
            {
                /// <inheritdoc />
                public Manifest Manifest { get; } = new()
                {
                    Id = "xihan.shop.orders",
                    Package = "XiHan.Shop.Orders",
                    Version = "1.0.0",
                    Primitives = 1,
                    Stability = Stability.Stable,
                    Kind = ManifestKind.Capability,
                    Requires =
                    [
                        new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
                        new CapabilityRequirement(PersistenceCapabilities.Relational, PersistenceCapabilities.Version),
                        new CapabilityRequirement(CachingCapabilities.Distributed, CachingCapabilities.Version),
                    ],
                    Entry = typeof(ShopOrdersFeature).FullName,
                };

                /// <inheritdoc />
                public void Configure(FeatureContext context)
                {
                    context.Services.Configure<OrderOptions>(options => options.Limit = 3);

                    var services = context.Services;
                    var configuration = context.Configuration;
                    services.AddOrders(configuration);
                }

                /// <inheritdoc />
                public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
                {
                    {{Markers.Line("V4-01", CliMessages.MigrateMarkerApplicationBuilder.L())}}
                    var app = context.GetApplicationBuilder();
                    app.UseOrders();
                    services.GetRequiredService<OrderWarmup>().Run(typeof(ShopOrdersFeature));

                    return Task.CompletedTask;
                }

                /// <inheritdoc />
                public Task StopAsync(CancellationToken cancellationToken)
                {
                    {{Markers.Line("V4-01", CliMessages.MigrateMarkerShutdownContext.L())}}
                    context.ServiceProvider.GetRequiredService<OrderWarmup>().Stop();

                    return Task.CompletedTask;
                }

                private static void Helper()
                {
                }
            }

            """;

        var (text, session) = Apply(new ModuleToFeatureFixer(), source, "Orders/XiHanShopOrdersModule.cs");

        Assert.Equal(Lf(expected), text);
        Assert.Equal(3, session.Findings.Count(static finding => finding.IsMarker));
        var rename = Assert.Single(session.Renames);
        Assert.EndsWith("XiHanShopOrdersModule.cs", rename.Key, StringComparison.Ordinal);
        Assert.EndsWith(Path.Combine("Orders", "ShopOrdersFeature.cs"), rename.Value, StringComparison.Ordinal);

        // 再跑一遍：已是特性类，零改动
        var (again, second) = Apply(new ModuleToFeatureFixer(), text);
        Assert.Equal(text, again);
        Assert.Empty(second.Findings);
    }

    [Fact]
    public void ModuleToFeature_WithoutStartOrStopStatements_IsPlainFeature()
    {
        const string source = """
            namespace Shop;

            public class XiHanShopModule : XiHanModule
            {
                public override void ConfigureServices(ServiceConfigurationContext context) => context.Services.AddShop();

                public override Task OnApplicationInitializationAsync(ApplicationInitializationContext context) => Task.CompletedTask;
            }

            """;

        var (text, _) = Apply(new ModuleToFeatureFixer(), source);

        Assert.Contains("public sealed class ShopFeature : IFeature\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("StartAsync", text, StringComparison.Ordinal);
        Assert.Contains("Id = \"xihan.app.shop\"", text, StringComparison.Ordinal);
        Assert.Contains("    public void Configure(FeatureContext context)\n    {\n        context.Services.AddShop();\n    }\n", text, StringComparison.Ordinal);
        Assert.StartsWith("using XiHan.Framework.Kernel;\nusing XiHan.Framework.Kernel.Hosting;\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void EntityProtocol_ExpandsBase_RenamesMembers_AndRetypesStampAndTenant()
    {
        const string source = """
            using XiHan.Framework.Data.SqlSugar.Entities;

            namespace Shop.Entities;

            public abstract class ShopEntity : SugarMultiTenantCreationEntity<long>, ITraceableEntity
            {
                protected ShopEntity(long basicId) : base(basicId)
                {
                }

                public string? TraceId { get; set; }
            }

            public sealed class OrderDto
            {
                public long BasicId { get; set; }

                public DateTimeOffset CreatedTime { get; set; }

                public long RowVersion { get; set; }

                public long? TenantId { get; set; }
            }

            """;
        var expected = $$"""
            using SqlSugar;
            using XiHan.Framework.Data.SqlSugar.Entities;
            using XiHan.Framework.Domain;

            namespace Shop.Entities;

            {{Markers.Line("V4-02", CliMessages.MigrateMarkerAppInterface.L(("name", "ITraceableEntity")))}}
            {{Markers.Line("V4-02", CliMessages.MigrateMarkerConcurrencyStampColumn.L())}}
            {{Markers.Line("V4-02", CliMessages.MigrateMarkerTenantIdType.L())}}
            public abstract class ShopEntity : IEntity<long>, IHasConcurrencyStamp, IHasCreationTime, IMultiTenant, ITraceableEntity
            {
                /// <inheritdoc />
                [SugarColumn(ColumnName = "Basic_Id", IsPrimaryKey = true, IsIdentity = false, ColumnDescription = "主键")]
                public long Id { get; set; } = default!;

                /// <inheritdoc />
                [SugarColumn(ColumnName = "Row_Version", Length = 32, ColumnDescription = "版本控制标识，用于处理并发")]
                public string ConcurrencyStamp { get; set; } = string.Empty;

                /// <inheritdoc />
                [SugarColumn(ColumnName = "Created_Time", IsNullable = false, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建时间")]
                public DateTimeOffset CreatedAt { get; set; }

                /// <summary>{{CliMessages.MigrateEntityCreatedId.L()}}</summary>
                [SugarColumn(ColumnName = "Created_Id", IsNullable = true, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建者唯一标识")]
                public long? CreatedId { get; set; }

                /// <summary>{{CliMessages.MigrateEntityCreatedBy.L()}}</summary>
                [SugarColumn(ColumnName = "Created_By", IsNullable = true, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建人")]
                public string? CreatedBy { get; set; }

                /// <inheritdoc />
                [SugarColumn(ColumnName = "Tenant_Id", IsOnlyIgnoreUpdate = true, ColumnDescription = "租户ID")]
                public string TenantId { get; set; } = string.Empty;

                protected ShopEntity(long basicId)
                {
                    Id = basicId;
                }

                public string? TraceId { get; set; }
            }

            public sealed class OrderDto
            {
                public long Id { get; set; }

                public DateTimeOffset CreatedAt { get; set; }

                {{Markers.Line("V4-02", CliMessages.MigrateMarkerConcurrencyStampColumn.L())}}
                public string ConcurrencyStamp { get; set; } = string.Empty;

                {{Markers.Line("V4-02", CliMessages.MigrateMarkerTenantIdType.L())}}
                public string? TenantId { get; set; }
            }

            """;

        var (text, session) = Apply(new EntityProtocolFixer(), source);

        Assert.Equal(Lf(expected), text);
        Assert.Equal(5, session.Findings.Count(static finding => finding.IsMarker));

        var (again, second) = Apply(new EntityProtocolFixer(), text);
        Assert.Equal(text, again);
        Assert.Empty(second.Findings);
    }

    [Fact]
    public void DependencyMarker_CollectsRegistrations_StripsMarkers_AndGeneratesServicesFile()
    {
        const string source = """
            using XiHan.Framework.Core.DependencyInjection;

            namespace Shop.Services;

            /// <summary>
            /// 订单服务
            /// </summary>
            public sealed class OrderService : IOrderService, ITransientDependency
            {
            }

            [ExposeServices(typeof(IPricing))]
            public sealed class PricingService : IPricing, IDisposable, IScopedDependency
            {
            }

            public sealed class Worker : ISingletonDependency
            {
            }

            [Dependency(ServiceLifetime.Scoped)]
            public sealed class Audit : IAudit
            {
            }

            public sealed class Cache<T> : ITransientDependency
            {
            }

            """;
        var expected = $$"""
            using XiHan.Framework.Core.DependencyInjection;

            namespace Shop.Services;

            /// <summary>
            /// 订单服务
            /// </summary>
            public sealed class OrderService : IOrderService
            {
            }

            public sealed class PricingService : IPricing, IDisposable
            {
            }

            public sealed class Worker
            {
            }

            public sealed class Audit : IAudit
            {
            }

            {{Markers.Line("V4-03", CliMessages.MigrateMarkerManualRegistration.L(("name", "Cache")))}}
            public sealed class Cache<T>
            {
            }

            """;
        var project = Path.Combine(Path.GetTempPath(), "Shop");

        var (text, session) = Apply(new DependencyMarkerFixer(), source, "Shop/Services/Services.cs", project);

        Assert.Equal(Lf(expected), text);
        var registrations = Assert.Single(session.Registrations).Value;
        Assert.Equal(
            [("OrderService", "Transient", "IOrderService"), ("PricingService", "Scoped", "IPricing"), ("Worker", "Singleton", ""), ("Audit", "Scoped", "IAudit")],
            registrations.Select(static r => (r.Implementation, r.Lifetime, string.Join(",", r.Services))));

        var marker = Markers.Line("V4-03", CliMessages.MigrateMarkerRegisterServices.L(("method", "AddShopV4Services")));
        var generated = Lf(V4ServicesGenerator.Generate("Shop", "ShopV4Services", registrations, marker, "\n"));
        var header = CliMessages.MigrateGenerated.L();
        Assert.Equal($$"""
            {{marker}}
            // {{header}}

            using Microsoft.Extensions.DependencyInjection;
            using Shop.Services;

            namespace Shop;

            /// <summary>
            /// {{header}}
            /// </summary>
            public static class ShopV4Services
            {
                /// <summary>
                /// {{header}}
                /// </summary>
                public static IServiceCollection AddShopV4Services(this IServiceCollection services)
                {
                    services.AddScoped<IAudit, Audit>();
                    services.AddTransient<IOrderService, OrderService>();
                    services.AddScoped<IPricing, PricingService>();
                    services.AddSingleton<Worker>();
                    return services;
                }
            }

            """.Replace("\r\n", "\n", StringComparison.Ordinal), generated);

        var (again, second) = Apply(new DependencyMarkerFixer(), text, "Shop/Services/Services.cs", project);
        Assert.Equal(text, again);
        Assert.Empty(second.Findings);
    }

    [Fact]
    public void Clock_RewritesTypeAndMembers()
    {
        const string source = """
            using XiHan.Framework.Timing;

            public sealed class Stamp(IClock clock)
            {
                private readonly IClock _clock = clock;

                public DateTimeOffset Now => _clock.Now;

                public DateTime Today => this._clock.Today;

                public DateTimeOffset Utc() => clock.UtcNow;

                public DateTimeOffset Other(Other other) => other.Now;
            }

            """;
        const string expected = """
            using XiHan.Framework.Timing;

            public sealed class Stamp(TimeProvider clock)
            {
                private readonly TimeProvider _clock = clock;

                public DateTimeOffset Now => _clock.GetUtcNow();

                public DateTime Today => this._clock.GetUtcNow().Date;

                public DateTimeOffset Utc() => clock.GetUtcNow();

                public DateTimeOffset Other(Other other) => other.Now;
            }

            """;

        var (text, session) = Apply(new ClockFixer(), source);

        Assert.Equal(Lf(expected), text);
        Assert.Equal(5, session.Findings.Count);
        Assert.Empty(Apply(new ClockFixer(), text).Session.Findings);
    }

    [Fact]
    public void UnitOfWork_StripsAttributes_AndMarksManager()
    {
        const string source = """
            using XiHan.Framework.Uow;

            [UnitOfWork]
            public sealed class OrderAppService(IUnitOfWorkManager unitOfWorkManager)
            {
                private readonly IUnitOfWorkManager _unitOfWorkManager = unitOfWorkManager;

                /// <summary>
                /// 创建
                /// </summary>
                [UnitOfWork(true)]
                public async Task CreateAsync()
                {
                    using var uow = _unitOfWorkManager.Begin(requiresNew: true);
                    await uow.CompleteAsync();
                }
            }

            """;
        var expected = $$"""
            using XiHan.Framework.Uow;

            public sealed class OrderAppService(IUnitOfWorkManager unitOfWorkManager)
            {
                {{Markers.Line("V4-05", CliMessages.MigrateMarkerUnitOfWorkManager.L())}}
                private readonly IUnitOfWorkManager _unitOfWorkManager = unitOfWorkManager;

                /// <summary>
                /// 创建
                /// </summary>
                public async Task CreateAsync()
                {
                    {{Markers.Line("V4-05", CliMessages.MigrateMarkerUnitOfWorkManager.L())}}
                    using var uow = _unitOfWorkManager.Begin(requiresNew: true);
                    await uow.CompleteAsync();
                }
            }

            """;

        var (text, session) = Apply(new UnitOfWorkFixer(), source);

        Assert.Equal(Lf(expected), text);
        Assert.Equal(2, session.Findings.Count(static finding => !finding.IsMarker));
        Assert.Equal(2, session.Findings.Count(static finding => finding.IsMarker));
        Assert.Empty(Apply(new UnitOfWorkFixer(), text).Session.Findings);
    }

    [Fact]
    public void PackageReference_Maps_Dedupes_BumpsSameNames_AndConvertsProjectReferences()
    {
        const string source = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="XiHan.Framework.Core" Version="4.8.0" />
                <PackageReference Include="XiHan.Framework.Utils" Version="4.8.0" />
                <PackageReference Include="XiHan.Framework.Web.Api" Version="4.8.0" />
                <PackageReference Include="XiHan.Framework.Domain" Version="4.8.0" />
                <PackageReference Include="XiHan.Framework.Http" Version="4.8.0" />
                <PackageReference Include="Serilog" Version="4.0.0" />
                <ProjectReference Include="..\..\framework\XiHan.Framework.Caching\XiHan.Framework.Caching.csproj" />
                <ProjectReference Include="..\Shop.Core\Shop.Core.csproj" />
              </ItemGroup>
            </Project>

            """;
        var expected = $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="XiHan.Framework.Kernel" Version="{{FrameworkInfo.Version}}" />
                <PackageReference Include="XiHan.Framework.Hosting.AspNetCore" Version="{{FrameworkInfo.Version}}" />
                <PackageReference Include="XiHan.Framework.Protocols.Http" Version="{{FrameworkInfo.Version}}" />
                <PackageReference Include="XiHan.Framework.Domain" Version="{{FrameworkInfo.Version}}" />
                <PackageReference Include="Serilog" Version="4.0.0" />
                <PackageReference Include="XiHan.Framework.Caching.Abstractions" Version="{{FrameworkInfo.Version}}" />
                <ProjectReference Include="..\Shop.Core\Shop.Core.csproj" />
              </ItemGroup>
            </Project>

            """;

        var (text, session) = Apply(new PackageReferenceFixer(), source, "Shop/Shop.csproj");

        Assert.Equal(Lf(expected), text);
        Assert.Equal(7, session.Findings.Count);
        Assert.Empty(Apply(new PackageReferenceFixer(), text, "Shop/Shop.csproj").Session.Findings);
    }

    [Fact]
    public void LegacyUsing_RemovesOnlyFourXNamespaces()
    {
        const string source = """
            // header
            using XiHan.Framework.Core.Modularity;
            using Shop.Core;
            using XiHan.Framework.Kernel.Hosting;
            using XiHan.Framework.Kernel.Security.Claims;
            using XiHan.Framework.Web.Core.Extensions;
            using XiHan.Framework.Persistence.SqlSugar;

            namespace Shop;

            """;
        const string expected = """
            // header
            using Shop.Core;
            using XiHan.Framework.Kernel.Hosting;
            using XiHan.Framework.Kernel.Security.Claims;
            using XiHan.Framework.Persistence.SqlSugar;

            namespace Shop;

            """;

        var (text, session) = Apply(new LegacyUsingFixer(), source);

        Assert.Equal(Lf(expected), text);
        Assert.Equal(2, session.Findings.Count);
        Assert.Empty(Apply(new LegacyUsingFixer(), text).Session.Findings);
    }

    [Fact]
    public void Migration_RunsAllRules_WritesServicesFile_Renames_SkipsBuild_AndIsIdempotent()
    {
        using var fixture = new MigrationFixture();

        var first = new V4Migration(fixture.Root, V4Rules.All, dryRun: false).Run(CancellationToken.None);

        Assert.Equal("v4", first.Version);
        Assert.False(first.DryRun);
        Assert.Equal(V4Rules.Ids, first.Rules.Select(static rule => rule.Id));
        Assert.True(File.Exists(Path.Combine(fixture.Project, "ShopOrdersFeature.cs")));
        Assert.False(File.Exists(Path.Combine(fixture.Project, "XiHanShopOrdersModule.cs")));
        Assert.Contains("public sealed class ShopOrdersFeature : IFeature", File.ReadAllText(Path.Combine(fixture.Project, "ShopOrdersFeature.cs")), StringComparison.Ordinal);
        Assert.Contains("services.AddTransient<IOrderService, OrderService>();", File.ReadAllText(Path.Combine(fixture.Project, "OrdersV4Services.cs")), StringComparison.Ordinal);
        Assert.Contains("XiHan.Framework.Kernel", File.ReadAllText(Path.Combine(fixture.Project, "Shop.Orders.csproj")), StringComparison.Ordinal);
        Assert.Equal(MigrationFixture.SkippedSource, File.ReadAllText(fixture.Skipped));
        Assert.Contains(first.Markers, static marker => marker is { Rule: "V4-03", File: "Shop.Orders/OrdersV4Services.cs", Line: 1 });
        Assert.All(first.Rules.Where(static rule => rule.Id is "V4-01" or "V4-03" or "V4-10"), static rule => Assert.True(rule.Changed > 0, rule.Id));

        // 再跑一遍：零改动、零标记
        var second = new V4Migration(fixture.Root, V4Rules.All, dryRun: false).Run(CancellationToken.None);
        Assert.All(second.Rules, static rule => Assert.Equal((0, 0), (rule.Changed, rule.Markers)));
        Assert.Empty(second.Markers);
    }

    [Fact]
    public void Migration_DryRun_ReportsWithoutWriting()
    {
        using var fixture = new MigrationFixture();
        var before = Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories).ToDictionary(static file => file, File.ReadAllText, StringComparer.Ordinal);

        var report = new V4Migration(fixture.Root, [new ModuleToFeatureFixer(), new DependencyMarkerFixer()], dryRun: true).Run(CancellationToken.None);

        Assert.True(report.DryRun);
        Assert.Equal(["V4-01", "V4-03"], report.Rules.Select(static rule => rule.Id));
        Assert.True(report.Rules[0].Changed > 0);
        Assert.Equal(before, Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories).ToDictionary(static file => file, File.ReadAllText, StringComparer.Ordinal));
    }
}
