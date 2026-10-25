// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Cli.Migration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Tests.Migration;

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

                    {{ModuleRegistrationsMarker("XiHanDataModule")}}
                    {{ModuleRegistrationsMarker("XiHanCachingModule")}}
                    {{ModuleRegistrationsMarker("XiHanHttpModule")}}
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

        // 三个被删框架模块各留一条丢失注册的清单标记（无承接方法可调用），加上无能力模块、应用构建器、关闭上下文三条
        Assert.Equal(Lf(expected), text);
        Assert.Equal(6, session.Findings.Count(static finding => finding.IsMarker));
        var rename = Assert.Single(session.Renames);
        Assert.EndsWith("XiHanShopOrdersModule.cs", rename.Key, StringComparison.Ordinal);
        Assert.EndsWith(Path.Combine("Orders", "ShopOrdersFeature.cs"), rename.Value, StringComparison.Ordinal);

        // 再跑一遍：已是特性类，零改动
        var (again, second) = Apply(new ModuleToFeatureFixer(), text);
        Assert.Equal(text, again);
        Assert.Empty(second.Findings);
    }

    /// <summary>
    /// 被删 4.x 模块丢失注册的清单标记（§4.1 的全部注册项）。
    /// </summary>
    private static string ModuleRegistrationsMarker(string module, params string[] excluded)
        => Markers.Line("V4-01", CliMessages.MigrateMarkerModuleRegistrations.L(
            ("module", module),
            ("registrations", string.Join("; ", V4Modules.Registrations(module).Where(registration => registration.Method is null || !excluded.Contains(registration.Method)).Select(static registration => registration.ToString())))));

    [Fact]
    public void ModuleToFeature_ListsDroppedModuleRegistrations_AndCallsCarriedOverExtensions()
    {
        const string extensions = """
            using Microsoft.Extensions.Configuration;
            using Microsoft.Extensions.DependencyInjection;

            namespace Shop.Core;

            public static class CoreServiceCollectionExtensions
            {
                public static IServiceCollection AddXiHanDistributedIds(this IServiceCollection services, IConfiguration configuration) => services;

                public static IServiceCollection AddXiHanTiming(this IServiceCollection services) => services;

                public static IServiceCollection AddXiHanTemplating(this IServiceCollection services) => services;
            }

            """;
        const string source = """
            namespace Shop.Core;

            [DependsOn(typeof(XiHanWebCoreModule), typeof(XiHanDistributedIdsModule), typeof(XiHanTimingModule), typeof(XiHanTemplatingModule), typeof(XiHanCastleModule))]
            public class XiHanShopCoreModule : XiHanModule
            {
                public override void ConfigureServices(ServiceConfigurationContext context)
                {
                    context.Services.AddXiHanTemplating();
                }
            }

            """;
        var expected = $$"""
            using XiHan.Framework.Kernel;
            using XiHan.Framework.Kernel.Hosting;
            using XiHan.Framework.Persistence.Abstractions;
            using XiHan.Framework.Primitives;
            using XiHan.Framework.Primitives.Manifests;
            using XiHan.Framework.Protocols.Http;

            namespace Shop.Core;

            {{Markers.Line("V4-01", CliMessages.MigrateMarkerModulesDropped.L(("modules", "XiHanTimingModule, XiHanTemplatingModule, XiHanCastleModule")))}}
            public sealed class ShopCoreFeature : IFeature
            {
                /// <inheritdoc />
                public Manifest Manifest { get; } = new()
                {
                    Id = "xihan.shop.core",
                    Package = "Shop.Core",
                    Version = "1.0.0",
                    Primitives = 1,
                    Stability = Stability.Stable,
                    Kind = ManifestKind.Capability,
                    Requires =
                    [
                        new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
                        new CapabilityRequirement(HttpProtocolCapabilities.Http, HttpProtocolCapabilities.Version),
                        new CapabilityRequirement(PersistenceCapabilities.Relational, PersistenceCapabilities.Version),
                    ],
                    Entry = typeof(ShopCoreFeature).FullName,
                };

                /// <inheritdoc />
                public void Configure(FeatureContext context)
                {
                    context.Services.AddXiHanTemplating();

                    context.Services.AddXiHanDistributedIds(context.Configuration);
                    context.Services.AddXiHanTiming();
                    {{ModuleRegistrationsMarker("XiHanWebCoreModule")}}
                }
            }

            """;
        var session = new MigrationSession();
        var fixer = new ModuleToFeatureFixer();
        var extensionsFile = new MigrationFile(Path.Combine(Path.GetTempPath(), "Shop.Core", "CoreServiceCollectionExtensions.cs"), "Shop.Core/CoreServiceCollectionExtensions.cs", null, "\n");
        var moduleFile = new MigrationFile(Path.Combine(Path.GetTempPath(), "Shop.Core", "XiHanShopCoreModule.cs"), "Shop.Core/XiHanShopCoreModule.cs", null, "\n");
        fixer.Scan(extensionsFile, Lf(extensions), session);
        fixer.Scan(moduleFile, Lf(source), session);

        var text = Lf(fixer.Apply(moduleFile, Lf(source), session));

        // 应用已承接的扩展方法直接生成调用（有 IConfiguration 参数才传配置）；原钩子体已调用的不再提；没有承接方法的模块留带清单的标记；Castle 没有应用会消费的注册
        Assert.Equal(Lf(expected), text);
        Assert.Equal(2, session.Findings.Count(static finding => finding.IsMarker));
        Assert.Equal(["AddXiHanDistributedIds", "AddXiHanTemplating", "AddXiHanTiming"], session.ServiceExtensions.Keys.Order(StringComparer.Ordinal));
        Assert.True(session.ServiceExtensions["AddXiHanDistributedIds"]);
        Assert.False(session.ServiceExtensions["AddXiHanTiming"]);
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
            using XiHan.Framework.Domain.Entities;

            namespace Shop.Entities;

            {{Markers.Line("V4-02", CliMessages.MigrateMarkerAppInterface.L(("name", "ITraceableEntity")))}}
            {{Markers.Line("V4-02", CliMessages.MigrateMarkerConcurrencyStampColumn.L())}}
            {{Markers.Line("V4-02", CliMessages.MigrateMarkerTenantIdType.L())}}
            public abstract class ShopEntity : IEntity<long>, IHasConcurrencyStamp, IHasCreationTime, IMultiTenant, ITraceableEntity
            {
                /// <inheritdoc />
                [SugarColumn(ColumnName = "Basic_Id", IsPrimaryKey = true, IsIdentity = false, ColumnDescription = "主键")]
                public virtual long Id { get; set; } = default!;

                /// <inheritdoc />
                [SugarColumn(ColumnName = "Row_Version", Length = 32, ColumnDescription = "版本控制标识，用于处理并发")]
                public virtual string ConcurrencyStamp { get; set; } = string.Empty;

                /// <inheritdoc />
                [SugarColumn(ColumnName = "Created_Time", IsNullable = false, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建时间")]
                public virtual DateTimeOffset CreatedAt { get; set; }

                /// <summary>{{CliMessages.MigrateEntityCreatedId.L()}}</summary>
                [SugarColumn(ColumnName = "Created_Id", IsNullable = true, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建者唯一标识")]
                public virtual long? CreatedId { get; set; }

                /// <summary>{{CliMessages.MigrateEntityCreatedBy.L()}}</summary>
                [SugarColumn(ColumnName = "Created_By", IsNullable = true, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建人")]
                public virtual string? CreatedBy { get; set; }

                /// <inheritdoc />
                [SugarColumn(ColumnName = "Tenant_Id", IsOnlyIgnoreUpdate = true, ColumnDescription = "租户ID")]
                public virtual string TenantId { get; set; } = string.Empty;

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
    public void DependencyMarker_RegistersClassesInheritingMarkersFromBases_WithSelfAndDefaultInterfaces()
    {
        const string repositories = """
            using XiHan.Framework.Core.DependencyInjection;
            using XiHan.Framework.Data.SqlSugar.Repository;

            namespace Shop.Infrastructure.Repositories;

            /// <summary>
            /// 聚合根仓储基类
            /// </summary>
            public abstract class ShopAggregateRepository<TAggregateRoot>(ISqlSugarClientResolver clientResolver)
                : SqlSugarAggregateRepository<TAggregateRoot, long>(clientResolver), IShopAggregateRepository<TAggregateRoot>, IScopedDependency
                where TAggregateRoot : class, new();

            public sealed class RoleRepository(ISqlSugarClientResolver clientResolver) : ShopAggregateRepository<SysRole>(clientResolver), IRoleRepository;

            """;
        const string services = """
            using XiHan.Framework.Application.Attributes;
            using XiHan.Framework.Application.Services;
            using XiHan.Framework.Domain.Services;

            namespace Shop.Application;

            public abstract class ShopApplicationService : ApplicationServiceBase;

            public sealed class ReportService : ShopApplicationService, IReportService, IDisposable
            {
            }

            [DynamicApi]
            public sealed class UserAppService : ShopApplicationService, IUserAppService
            {
            }

            public sealed class OrderManager : DomainService, IOrderManager, IHandler<SysOrder>
            {
            }

            public sealed class Plain : IPlain
            {
            }

            """;
        const string expectedRepositories = """
            using XiHan.Framework.Core.DependencyInjection;
            using XiHan.Framework.Data.SqlSugar.Repository;

            namespace Shop.Infrastructure.Repositories;

            /// <summary>
            /// 聚合根仓储基类
            /// </summary>
            public abstract class ShopAggregateRepository<TAggregateRoot>(ISqlSugarClientResolver clientResolver)
                : SqlSugarAggregateRepository<TAggregateRoot, long>(clientResolver), IShopAggregateRepository<TAggregateRoot>
                where TAggregateRoot : class, new();

            public sealed class RoleRepository(ISqlSugarClientResolver clientResolver) : ShopAggregateRepository<SysRole>(clientResolver), IRoleRepository;

            """;
        var project = Path.Combine(Path.GetTempPath(), "Shop");
        var repositoriesFile = new MigrationFile(Path.Combine(project, "Infrastructure", "Repositories", "Repositories.cs"), "Shop/Infrastructure/Repositories/Repositories.cs", project, "\n");
        var servicesFile = new MigrationFile(Path.Combine(project, "Application", "Services.cs"), "Shop/Application/Services.cs", project, "\n");
        var session = new MigrationSession();
        var fixer = new DependencyMarkerFixer();
        var dynamicApi = new DynamicApiFixer();
        foreach (var (file, text) in new[] { (repositoriesFile, repositories), (servicesFile, services) })
        {
            fixer.Scan(file, Lf(text), session);
            dynamicApi.Scan(file, Lf(text), session);
        }

        fixer.Prepare(session);
        var repositoriesText = Lf(fixer.Apply(repositoriesFile, Lf(repositories), session));
        var servicesText = Lf(fixer.Apply(servicesFile, Lf(services), session));

        // 抽象基类只去掉标记、不留标记；具体派生类按继承到的存期登记：类本身 + 类名以「接口名去 I」结尾的接口（基类上带类型参数的接口、IDisposable 与不匹配的接口不算）；
        // 动态 API 服务类由 V4-06 登记；没有任何存期来源的类不登记
        Assert.Equal(Lf(expectedRepositories), repositoriesText);
        Assert.Equal(Lf(services), servicesText);
        Assert.DoesNotContain(session.Findings, static finding => finding.IsMarker);
        var registrations = Assert.Single(session.Registrations).Value;
        Assert.Equal(
            [("OrderManager", "Transient", "IOrderManager", true), ("ReportService", "Transient", "IReportService", true), ("RoleRepository", "Scoped", "IRoleRepository", true)],
            registrations.Select(static r => (r.Implementation, r.Lifetime, string.Join(",", r.Services), r.IncludeSelf)));

        var generated = Lf(V4ServicesGenerator.Generate("Shop", "ShopV4Services", registrations, "// marker", "\n"));
        Assert.Contains("using Microsoft.Extensions.DependencyInjection;\nusing Microsoft.Extensions.DependencyInjection.Extensions;\nusing Shop.Application;\nusing Shop.Infrastructure.Repositories;\n", generated, StringComparison.Ordinal);
        Assert.Contains(
            "        services.TryAddTransient<OrderManager>();\n        services.TryAddTransient<IOrderManager>(static sp => sp.GetRequiredService<OrderManager>());\n" +
            "        services.TryAddTransient<ReportService>();\n        services.TryAddTransient<IReportService>(static sp => sp.GetRequiredService<ReportService>());\n" +
            "        services.TryAddScoped<RoleRepository>();\n        services.TryAddScoped<IRoleRepository>(static sp => sp.GetRequiredService<RoleRepository>());\n        return services;\n",
            generated,
            StringComparison.Ordinal);

        // 再跑一遍：标记已去掉，零改动
        var second = new MigrationSession();
        fixer.Scan(repositoriesFile, repositoriesText, second);
        fixer.Scan(servicesFile, servicesText, second);
        fixer.Prepare(second);
        Assert.Equal(repositoriesText, Lf(fixer.Apply(repositoriesFile, repositoriesText, second)));
        Assert.Equal(servicesText, Lf(fixer.Apply(servicesFile, servicesText, second)));
        Assert.Empty(second.Findings);
    }

    [Fact]
    public void DynamicApi_GeneratesDelegatingOperations_AndStripsServices()
    {
        const string baseSource = """
            using Microsoft.AspNetCore.Authorization;
            using XiHan.Framework.Application.Attributes;
            using XiHan.Framework.Application.Services;

            namespace Shop.Application;

            [Authorize]
            [DynamicApi(Group = "Shop", GroupName = "商城")]
            public abstract class ShopApplicationService : ApplicationServiceBase
            {
            }

            """;
        const string serviceSource = """
            using Microsoft.AspNetCore.Authorization;
            using Microsoft.AspNetCore.Mvc;
            using Shop.Application.Dtos;
            using Shop.Domain.Permissions;
            using XiHan.Framework.Authorization.AspNetCore;

            namespace Shop.Application.Orders;

            /// <summary>
            /// 订单服务
            /// </summary>
            [DynamicApi(Tag = "订单")]
            public sealed class OrderAppService : ShopApplicationService, IOrderAppService
            {
                private readonly IOrderRepository _orders;

                public OrderAppService(IOrderRepository orders)
                {
                    _orders = orders;
                }

                [HttpPost]
                [PermissionAuthorize(ShopPermissionCodes.Order.Create)]
                public async Task<OrderDetailDto> CreateOrderAsync(OrderCreateDto input, CancellationToken cancellationToken = default)
                {
                    Logger.LogInformation("create");
                    return await _orders.CreateAsync(input, cancellationToken);
                }

                public Task<OrderDetailDto> GetOrderAsync(long id, CancellationToken cancellationToken = default) => _orders.GetAsync(id, cancellationToken);

                public Task<PagedResult<OrderListDto>> GetOrderPageAsync(OrderPageQueryDto query, CancellationToken cancellationToken = default) => _orders.PageAsync(query, cancellationToken);

                [PermissionAuthorize(ShopPermissionCodes.Order.Status)]
                public Task UpdateOrderStatusAsync(OrderStatusUpdateDto input, CancellationToken cancellationToken = default) => _orders.StatusAsync(input, cancellationToken);

                [AllowAnonymous]
                public int Count() => _orders.Count;

                public Task<FileContentResult> ExportAsync(CancellationToken cancellationToken = default) => _orders.ExportAsync(cancellationToken);

                private Task HelperAsync() => Task.CompletedTask;
            }

            """;
        var expectedService = $$"""
            using Microsoft.AspNetCore.Authorization;
            using Microsoft.AspNetCore.Mvc;
            using Shop.Application.Dtos;
            using Shop.Domain.Permissions;
            using XiHan.Framework.Authorization.AspNetCore;

            namespace Shop.Application.Orders;

            /// <summary>
            /// 订单服务
            /// </summary>
            public sealed class OrderAppService : ShopApplicationService, IOrderAppService
            {
                private readonly IOrderRepository _orders;

                public OrderAppService(IOrderRepository orders)
                {
                    _orders = orders;
                }

                public async Task<OrderDetailDto> CreateOrderAsync(OrderCreateDto input, CancellationToken cancellationToken = default)
                {
                    {{Markers.Line("V4-06", CliMessages.MigrateMarkerServiceBaseMember.L())}}
                    Logger.LogInformation("create");
                    return await _orders.CreateAsync(input, cancellationToken);
                }

                public Task<OrderDetailDto> GetOrderAsync(long id, CancellationToken cancellationToken = default) => _orders.GetAsync(id, cancellationToken);

                public Task<PagedResult<OrderListDto>> GetOrderPageAsync(OrderPageQueryDto query, CancellationToken cancellationToken = default) => _orders.PageAsync(query, cancellationToken);

                public Task UpdateOrderStatusAsync(OrderStatusUpdateDto input, CancellationToken cancellationToken = default) => _orders.StatusAsync(input, cancellationToken);

                public int Count() => _orders.Count;

                {{Markers.Line("V4-06", CliMessages.MigrateMarkerUnsupportedOperation.L(("name", "ExportAsync")))}}
                public Task<FileContentResult> ExportAsync(CancellationToken cancellationToken = default) => _orders.ExportAsync(cancellationToken);

                private Task HelperAsync() => Task.CompletedTask;
            }

            """;
        var header = CliMessages.MigrateGenerated.L();
        var expectedCreate = $$"""
            // {{header}}

            using Microsoft.AspNetCore.Authorization;
            using Microsoft.AspNetCore.Mvc;
            using Shop.Application.Dtos;
            using Shop.Application.Orders;
            using Shop.Domain.Permissions;
            using Shop.Operations;
            using XiHan.Framework.Kernel;
            using XiHan.Framework.Kernel.Operations;
            using XiHan.Framework.Primitives;

            namespace Shop.Operations.Order;

            /// <summary>
            /// {{header}}
            /// </summary>
            [Operation("order.create", Permission = ShopPermissionCodes.Order.Create)]
            public sealed class CreateOrderOperation(IOrderAppService service) : IOperation<OrderCreateDto, OrderDetailDto>
            {
                /// <inheritdoc />
                public async ValueTask<Result<OrderDetailDto>> HandleAsync(OrderCreateDto request, InvocationContext context)
                    => await service.CreateOrderAsync(request, context.CancellationToken).ConfigureAwait(false);
            }

            """;
        var expectedGet = $$"""
            [Operation("order.get", Policy = "authenticated")]
            public sealed class GetOrderOperation(IOrderAppService service) : IOperation<GetOrderRequest, OrderDetailDto>
            {
                /// <inheritdoc />
                public async ValueTask<Result<OrderDetailDto>> HandleAsync(GetOrderRequest request, InvocationContext context)
                    => await service.GetOrderAsync(request.Id, context.CancellationToken).ConfigureAwait(false);
            }

            /// <summary>
            /// {{header}}
            /// </summary>
            public sealed record GetOrderRequest(long Id);

            """;

        var root = Path.Combine(Path.GetTempPath(), "xihan-operations-" + Guid.NewGuid().ToString("N"));
        var project = Path.Combine(root, "Shop");
        Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, "Shop.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        try
        {
            var session = new MigrationSession();
            var fixer = new DynamicApiFixer();
            var baseFile = new MigrationFile(Path.Combine(project, "Application", "ShopApplicationService.cs"), "Shop/Application/ShopApplicationService.cs", project, "\n");
            var serviceFile = new MigrationFile(Path.Combine(project, "Application", "Orders", "OrderAppService.cs"), "Shop/Application/Orders/OrderAppService.cs", project, "\n");
            fixer.Scan(baseFile, Lf(baseSource), session);
            fixer.Scan(serviceFile, Lf(serviceSource), session);

            var baseText = Lf(fixer.Apply(baseFile, Lf(baseSource), session));
            var serviceText = Lf(fixer.Apply(serviceFile, Lf(serviceSource), session));
            V4OperationsGenerator.Complete(session, root);

            Assert.Equal("using Microsoft.AspNetCore.Authorization;\nusing XiHan.Framework.Application.Attributes;\nusing XiHan.Framework.Application.Services;\n\nnamespace Shop.Application;\n\npublic abstract class ShopApplicationService\n{\n}\n", baseText);
            Assert.Equal(Lf(expectedService), serviceText);

            string Generated(params string[] segments) => Lf(session.GeneratedFiles[Path.Combine([project, "Operations", .. segments])]);
            Assert.Equal(Lf(expectedCreate), Generated("Order", "CreateOrderOperation.cs"));
            Assert.EndsWith(Lf(expectedGet), Generated("Order", "GetOrderOperation.cs"), StringComparison.Ordinal);
            Assert.Contains("[Operation(\"order.list\", Policy = \"authenticated\")]\npublic sealed class GetOrderPageOperation(IOrderAppService service) : IOperation<OrderPageQueryDto, PagedResult<OrderListDto>>", Generated("Order", "GetOrderPageOperation.cs"), StringComparison.Ordinal);
            var status = Generated("Order", "UpdateOrderStatusOperation.cs");
            Assert.Contains("[Operation(\"order.update-status\", Permission = ShopPermissionCodes.Order.Status)]", status, StringComparison.Ordinal);
            Assert.Contains("IOperation<OrderStatusUpdateDto, EmptyResponse>\n{\n    /// <inheritdoc />\n    public async ValueTask<Result<EmptyResponse>> HandleAsync(OrderStatusUpdateDto request, InvocationContext context)\n    {\n        await service.UpdateOrderStatusAsync(request, context.CancellationToken).ConfigureAwait(false);\n        return EmptyResponse.Instance;\n    }\n}\n", status, StringComparison.Ordinal);
            var count = Generated("Order", "CountOperation.cs");
            Assert.Contains("[Operation(\"order.count\")]\npublic sealed class CountOperation(IOrderAppService service) : IOperation<CountRequest, int>", count, StringComparison.Ordinal);
            Assert.Contains("    public ValueTask<Result<int>> HandleAsync(CountRequest request, InvocationContext context)\n        => new(Result.Success(service.Count()));\n", count, StringComparison.Ordinal);
            Assert.EndsWith("public sealed record CountRequest();\n", count, StringComparison.Ordinal);
            Assert.False(session.GeneratedFiles.ContainsKey(Path.Combine(project, "Operations", "Order", "ExportOperation.cs")));

            Assert.Contains("public static EmptyResponse Instance { get; } = new();", Generated("EmptyResponse.cs"), StringComparison.Ordinal);
            var context = Generated("ShopOperationsJsonContext.cs");
            Assert.StartsWith(Markers.Line("V4-06", CliMessages.MigrateMarkerAddOperations.L()) + "\n", context, StringComparison.Ordinal);
            // 生成的类型写全限定名，不靠资源子命名空间的 using 解析（不同资源的同名记录会歧义）
            Assert.DoesNotContain("using Shop.Operations.Order;\n", context, StringComparison.Ordinal);
            foreach (var type in new[] { "Shop.Operations.Order.CountRequest", "Shop.Operations.EmptyResponse", "Shop.Operations.Order.GetOrderRequest", "OrderCreateDto", "OrderDetailDto", "OrderPageQueryDto", "OrderStatusUpdateDto", "PagedResult<OrderListDto>", "int" })
            {
                Assert.Contains($"[JsonSerializable(typeof({type}))]\n", context, StringComparison.Ordinal);
            }

            Assert.EndsWith("internal sealed partial class ShopOperationsJsonContext : JsonSerializerContext;\n", context, StringComparison.Ordinal);
            var registration = Assert.Single(session.Registrations[project]);
            Assert.Equal(("Shop.Application.Orders", "OrderAppService", "Scoped", "IOrderAppService"), (registration.Namespace, registration.Implementation, registration.Lifetime, string.Join(",", registration.Services)));
            Assert.Equal(3, session.Findings.Count(static finding => finding.IsMarker));

            // 再跑一遍：特性已删、目录里不再是动态 API，零改动
            var second = new MigrationSession();
            fixer.Scan(baseFile, baseText, second);
            fixer.Scan(serviceFile, serviceText, second);
            Assert.Equal(baseText, fixer.Apply(baseFile, baseText, second));
            Assert.Equal(serviceText, fixer.Apply(serviceFile, serviceText, second));
            Assert.Empty(second.Findings);
            Assert.Empty(second.GeneratedFiles);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CurrentContext_GeneratesWrappersInLowestProject_AddsUsings_AndMarksChange()
    {
        var root = Path.Combine(Path.GetTempPath(), "xihan-current-" + Guid.NewGuid().ToString("N"));
        var core = Path.Combine(root, "Shop.Core");
        var orders = Path.Combine(root, "Shop.Orders");
        Directory.CreateDirectory(Path.Combine(core, "Services"));
        Directory.CreateDirectory(Path.Combine(orders, "Services"));
        try
        {
            File.WriteAllText(Path.Combine(core, "Shop.Core.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
            File.WriteAllText(Path.Combine(orders, "Shop.Orders.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <ItemGroup>\n    <ProjectReference Include=\"..\\Shop.Core\\Shop.Core.csproj\" />\n  </ItemGroup>\n</Project>\n");
            File.WriteAllText(Path.Combine(core, "Services", "Auditor.cs"), "using XiHan.Framework.Security.Users;\n\nnamespace Shop.Core.Services;\n\npublic sealed class Auditor(ICurrentUser currentUser)\n{\n    public string? Actor => currentUser.UserName;\n}\n");
            File.WriteAllText(Path.Combine(orders, "Services", "OrderService.cs"), "using XiHan.Framework.MultiTenancy.Abstractions;\nusing XiHan.Framework.Security.Users;\n\nnamespace Shop.Orders.Services;\n\npublic sealed class OrderService(ICurrentTenant currentTenant, ICurrentUser currentUser)\n{\n    public void Run(string tenantId)\n    {\n        using var scope = currentTenant.Change(tenantId);\n        _ = currentUser.UserId;\n    }\n}\n");
            File.WriteAllText(Path.Combine(orders, "Services", "Exporter.cs"), "using System.Security.Claims;\nusing XiHan.Framework.Security.Claims;\n\nnamespace Shop.Orders.Services;\n\npublic sealed class Exporter(ICurrentPrincipalAccessor principalAccessor)\n{\n    public void RunAs(ClaimsPrincipal principal)\n    {\n        using var scope = principalAccessor.Change(principal);\n    }\n}\n");

            var first = new V4Migration(root, [new CurrentContextFixer()], dryRun: false).Run(CancellationToken.None);

            // 提供者是引用链最底层的使用项目 Shop.Core；Shop.Orders 不生成
            foreach (var name in V4CurrentContextGenerator.FileNames)
            {
                Assert.True(File.Exists(Path.Combine(core, "Security", name)), name);
            }

            Assert.False(Directory.Exists(Path.Combine(orders, "Security")));
            var currentUser = Lf(File.ReadAllText(Path.Combine(core, "Security", "CurrentUser.cs")));
            Assert.Contains("namespace Shop.Core.Security;\n", currentUser, StringComparison.Ordinal);
            Assert.Contains("public sealed class CurrentUser(IInvocationAccessor accessor) : ICurrentUser", currentUser, StringComparison.Ordinal);
            // 后台任务以指定用户身份执行（PrincipalAmbient.Override）时优先读覆盖主体
            Assert.Contains("private Principal Principal => PrincipalAmbient.Current is { } ambient ? PrincipalConversion.FromClaimsPrincipal(ambient) : Security.Principal;", currentUser, StringComparison.Ordinal);
            // 4.x 用 JwtSecurityTokenHandler 签发：角色与用户名在令牌里是短名 role / unique_name，5.0 不做入站映射，长短名都认
            Assert.Contains("public string[] Roles => [.. Principal.ClaimValues(ClaimTypes.Role).Concat(Principal.ClaimValues(RoleShortClaim)).Distinct(StringComparer.Ordinal)];", currentUser, StringComparison.Ordinal);
            Assert.Contains("public bool IsInRole(string roleName) => Principal.HasClaim(ClaimTypes.Role, roleName) || Principal.HasClaim(RoleShortClaim, roleName);", currentUser, StringComparison.Ordinal);
            Assert.Contains("public string? UserName => Find(ClaimTypes.Name) ?? Find(UniqueNameShortClaim);", currentUser, StringComparison.Ordinal);
            Assert.Contains("private const string RoleShortClaim = \"role\";\n    private const string UniqueNameShortClaim = \"unique_name\";\n", currentUser, StringComparison.Ordinal);
            var principalAccessor = Lf(File.ReadAllText(Path.Combine(core, "Security", "CurrentPrincipalAccessor.cs")));
            Assert.Contains("public sealed class CurrentPrincipalAccessor(IInvocationAccessor accessor) : ICurrentPrincipalAccessor", principalAccessor, StringComparison.Ordinal);
            Assert.Contains("    public ClaimsPrincipal Principal => PrincipalAmbient.Current\n        ?? PrincipalConversion.ToClaimsPrincipal(accessor.Current.TryGetValue(out var context) ? context.GetSecurity().Principal : XiHan.Framework.Kernel.Security.Principal.Anonymous);\n", principalAccessor, StringComparison.Ordinal);
            Assert.Contains("public IDisposable Change(ClaimsPrincipal principal) => PrincipalAmbient.Override(principal);", principalAccessor, StringComparison.Ordinal);
            Assert.Contains("IDisposable Change(ClaimsPrincipal principal);", File.ReadAllText(Path.Combine(core, "Security", "ICurrentPrincipalAccessor.cs")), StringComparison.Ordinal);
            var ambient = Lf(File.ReadAllText(Path.Combine(core, "Security", "PrincipalAmbient.cs")));
            Assert.Contains("private static readonly AsyncLocal<ClaimsPrincipal?> _override = new();", ambient, StringComparison.Ordinal);
            Assert.Contains("    public static IDisposable Override(ClaimsPrincipal principal)\n    {\n        ArgumentNullException.ThrowIfNull(principal);\n        var previous = _override.Value;\n        _override.Value = principal;\n        return new Restore(previous);\n    }\n", ambient, StringComparison.Ordinal);
            var conversion = Lf(File.ReadAllText(Path.Combine(core, "Security", "PrincipalConversion.cs")));
            Assert.Contains("public static ClaimsPrincipal ToClaimsPrincipal(Principal principal)", conversion, StringComparison.Ordinal);
            Assert.Contains("var id = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst(SubjectClaim)?.Value ?? principal.FindFirst(NameIdShortClaim)?.Value;", conversion, StringComparison.Ordinal);
            // ICurrentPrincipalAccessor 的使用文件同样补 using
            Assert.StartsWith("using Shop.Core.Security;\nusing System.Security.Claims;\n", Lf(File.ReadAllText(Path.Combine(orders, "Services", "Exporter.cs"))), StringComparison.Ordinal);
            Assert.Contains("public string? TenantId => Security.Tenant?.Id ?? Find(TenantIdClaim);", currentUser, StringComparison.Ordinal);
            // 主体 ID 原文给 Subject，UserId 仍是 long?（应用按数字主键使用）
            Assert.Contains("public string? Subject => Principal.IsAuthenticated ? Principal.Id : null;", currentUser, StringComparison.Ordinal);
            Assert.Contains("public long? UserId => long.TryParse(Subject, NumberStyles.None, CultureInfo.InvariantCulture, out var userId) ? userId : null;", currentUser, StringComparison.Ordinal);
            var currentUserInterface = File.ReadAllText(Path.Combine(core, "Security", "ICurrentUser.cs"));
            Assert.Contains("long? UserId { get; }", currentUserInterface, StringComparison.Ordinal);
            Assert.Contains("string? Subject { get; }", currentUserInterface, StringComparison.Ordinal);
            Assert.Contains("IDisposable Change(string? id, string? name = null);", File.ReadAllText(Path.Combine(core, "Security", "ICurrentTenant.cs")), StringComparison.Ordinal);
            // Change 用 AsyncLocal 覆盖栈实现，不再抛 NotSupportedException
            var currentTenant = Lf(File.ReadAllText(Path.Combine(core, "Security", "CurrentTenant.cs")));
            Assert.DoesNotContain("NotSupportedException", currentTenant, StringComparison.Ordinal);
            Assert.Contains("private static readonly AsyncLocal<Override?> _override = new();", currentTenant, StringComparison.Ordinal);
            Assert.Contains("public string? Id => _override.Value is { } current ? current.Id : Invoked?.Id;", currentTenant, StringComparison.Ordinal);
            Assert.Contains("        _override.Value = new Override(string.IsNullOrWhiteSpace(id) ? null : id.Trim(), name, parent);\n        return new Restore(parent);\n", currentTenant, StringComparison.Ordinal);

            Assert.StartsWith("using Shop.Core.Security;\nusing XiHan.Framework.Security.Users;\n", Lf(File.ReadAllText(Path.Combine(core, "Services", "Auditor.cs"))), StringComparison.Ordinal);
            var orderService = Lf(File.ReadAllText(Path.Combine(orders, "Services", "OrderService.cs")));
            Assert.StartsWith("using Shop.Core.Security;\nusing XiHan.Framework.MultiTenancy.Abstractions;\n", orderService, StringComparison.Ordinal);
            // Change 的调用点原样保留，不留标记
            Assert.Contains("    {\n        using var scope = currentTenant.Change(tenantId);\n", orderService, StringComparison.Ordinal);
            Assert.DoesNotContain("xihan-migrate", orderService, StringComparison.Ordinal);
            var services = File.ReadAllText(Path.Combine(core, "CoreV4Services.cs"));
            Assert.Contains("services.AddScoped<ICurrentTenant, CurrentTenant>();", services, StringComparison.Ordinal);
            Assert.Contains("services.AddScoped<ICurrentUser, CurrentUser>();", services, StringComparison.Ordinal);
            Assert.Contains("services.AddScoped<ICurrentPrincipalAccessor, CurrentPrincipalAccessor>();", services, StringComparison.Ordinal);
            var rule = Assert.Single(first.Rules);
            Assert.Equal(("V4-07", 0), (rule.Id, rule.Markers));
            Assert.Contains("Shop.Core/Security/CurrentUser.cs", rule.Files);

            var second = new V4Migration(root, [new CurrentContextFixer()], dryRun: false).Run(CancellationToken.None);
            Assert.All(second.Rules, static rule => Assert.Equal((0, 0), (rule.Changed, rule.Markers)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LocalEvent_RewritesHandlers_Publishers_AndRegistersByTopic()
    {
        const string source = """
            using Microsoft.Extensions.Logging;
            using Shop.Events;
            using XiHan.Framework.EventBus.Abstractions.Local;

            namespace Shop.Handlers;

            /// <summary>
            /// 登录事件处理器
            /// </summary>
            public sealed class LoginEventHandler(ILogger<LoginEventHandler> logger) : ILocalEventHandler<LoginSucceededDomainEvent>, ILocalEventHandler<LoginFailedDomainEvent>
            {
                public async Task HandleEventAsync(LoginSucceededDomainEvent eventData)
                {
                    await Task.Yield();
                    logger.LogInformation("ok {User}", eventData.UserId);
                }

                public Task HandleEventAsync(LoginFailedDomainEvent eventData) => Task.CompletedTask;
            }

            public sealed class LoginService(ILocalEventBus localEventBus)
            {
                public async Task RecordAsync(string user, CancellationToken cancellationToken)
                {
                    await localEventBus.PublishAsync(new LoginSucceededDomainEvent(user));
                    var failure = new LoginFailedDomainEvent(user);
                    await localEventBus.PublishAsync(failure);
                }
            }

            """;
        var generated = CliMessages.MigrateGenerated.L();
        var json = Markers.Line("V4-08", CliMessages.MigrateMarkerEventJson.L());
        var expected = $$"""
            using Microsoft.Extensions.Logging;
            using Shop.Events;
            using System.Text.Json;
            using XiHan.Framework.EventBus.Abstractions.Local;
            using XiHan.Framework.Messaging.Abstractions.Envelopes;
            using XiHan.Framework.Messaging.Abstractions.Handlers;
            using XiHan.Framework.Messaging.Abstractions.Publishing;
            using XiHan.Framework.Primitives;

            namespace Shop.Handlers;

            /// <summary>
            /// 登录事件处理器
            /// </summary>
            public sealed class LoginEventHandler(ILogger<LoginEventHandler> logger) : IMessageHandler
            {
                /// <summary>{{generated}}</summary>
                public const string LoginSucceededTopic = "login-succeeded";

                /// <summary>{{generated}}</summary>
                public const string LoginFailedTopic = "login-failed";

                /// <inheritdoc />
                public async ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context, CancellationToken cancellationToken)
                {
                    {{json}}
                    switch (delivery.Topic)
                    {
                        case LoginSucceededTopic:
                            await HandleEventAsync(JsonSerializer.Deserialize<LoginSucceededDomainEvent>(delivery.Envelope.Data.Span)!).ConfigureAwait(false);
                            return Result.Success();
                        case LoginFailedTopic:
                            await HandleEventAsync(JsonSerializer.Deserialize<LoginFailedDomainEvent>(delivery.Envelope.Data.Span)!).ConfigureAwait(false);
                            return Result.Success();
                        default:
                            return Result.Success();
                    }
                }

                public async Task HandleEventAsync(LoginSucceededDomainEvent eventData)
                {
                    await Task.Yield();
                    logger.LogInformation("ok {User}", eventData.UserId);
                }

                public Task HandleEventAsync(LoginFailedDomainEvent eventData) => Task.CompletedTask;
            }

            public sealed class LoginService(IMessagePublisher localEventBus)
            {
                public async Task RecordAsync(string user, CancellationToken cancellationToken)
                {
                    {{json}}
                    await localEventBus.PublishAsync("login-succeeded", new MessageEnvelope { Id = Guid.NewGuid().ToString("N"), Source = "/shop", Type = "login-succeeded", Time = DateTimeOffset.UtcNow, Data = JsonSerializer.SerializeToUtf8Bytes(new LoginSucceededDomainEvent(user)) }, cancellationToken);
                    var failure = new LoginFailedDomainEvent(user);
                    {{json}}
                    await localEventBus.PublishAsync("login-failed", new MessageEnvelope { Id = Guid.NewGuid().ToString("N"), Source = "/shop", Type = "login-failed", Time = DateTimeOffset.UtcNow, Data = JsonSerializer.SerializeToUtf8Bytes(failure) }, cancellationToken);
                }
            }

            """;

        using var project = new TempProject("Shop");
        var (text, session) = Apply(new LocalEventFixer(), source, "Shop/Handlers/LoginEventHandler.cs", project.Directory);

        Assert.Equal(Lf(expected), text);
        Assert.Equal(3, session.Findings.Count(static finding => finding.IsMarker));
        var registration = Assert.Single(session.Registrations[project.Directory]);
        Assert.Equal(["LoginEventHandler.LoginSucceededTopic", "LoginEventHandler.LoginFailedTopic"], registration.Topics);
        var services = V4ServicesGenerator.Generate("Shop", "ShopV4Services", session.Registrations[project.Directory], "// marker", "\n");
        Assert.Contains("using XiHan.Framework.Messaging.Abstractions;\n", services, StringComparison.Ordinal);
        Assert.Contains("        services.AddMessageHandler<LoginEventHandler>(LoginEventHandler.LoginSucceededTopic);\n        services.AddMessageHandler<LoginEventHandler>(LoginEventHandler.LoginFailedTopic);\n", services, StringComparison.Ordinal);

        var (again, second) = Apply(new LocalEventFixer(), text, "Shop/Handlers/LoginEventHandler.cs", project.Directory);
        Assert.Equal(text, again);
        Assert.Empty(second.Findings);
    }

    [Fact]
    public void EntityProtocol_RenamesDeclarationIdentifiers()
    {
        const string source = """
            namespace Shop.Commands;

            public sealed record OrderUpdateCommand(long BasicId, long RowVersion, DateTimeOffset CreatedTime);

            public static class OrderQueries
            {
                public static long Pick(OrderUpdateCommand command)
                {
                    var BasicId = command.BasicId;
                    return BasicId;
                }
            }

            """;
        const string expected = """
            namespace Shop.Commands;

            public sealed record OrderUpdateCommand(long Id, long ConcurrencyStamp, DateTimeOffset CreatedAt);

            public static class OrderQueries
            {
                public static long Pick(OrderUpdateCommand command)
                {
                    var Id = command.Id;
                    return Id;
                }
            }

            """;

        var (text, session) = Apply(new EntityProtocolFixer(), source);

        // 记录位置参数、局部变量与标识符用法一并改名，不留标记
        Assert.Equal(Lf(expected), text);
        Assert.NotEmpty(session.Findings);
        Assert.DoesNotContain(session.Findings, static finding => finding.IsMarker);

        var (again, second) = Apply(new EntityProtocolFixer(), text);
        Assert.Equal(text, again);
        Assert.Empty(second.Findings);
    }

    [Fact]
    public void DynamicApi_WrapsInterfaceReturns_AndQualifiesDuplicateRequestRecords()
    {
        const string orderSource = """
            using Shop.Application.Dtos;
            using XiHan.Framework.Application.Attributes;

            namespace Shop.Application.Orders;

            [DynamicApi]
            public sealed class OrderAppService
            {
                public Task<IReadOnlyList<OrderListDto>> GetOrdersAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<OrderListDto>>([]);

                public Task DeleteAsync(long id, CancellationToken cancellationToken = default) => Task.CompletedTask;
            }

            """;
        const string userSource = """
            using XiHan.Framework.Application.Attributes;

            namespace Shop.Application.Users;

            [DynamicApi]
            public sealed class UserAppService
            {
                public Task DeleteAsync(long id, CancellationToken cancellationToken = default) => Task.CompletedTask;
            }

            """;

        var root = Path.Combine(Path.GetTempPath(), "xihan-operations-" + Guid.NewGuid().ToString("N"));
        var project = Path.Combine(root, "Shop");
        Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, "Shop.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        try
        {
            var session = new MigrationSession();
            var fixer = new DynamicApiFixer();
            var orderFile = new MigrationFile(Path.Combine(project, "Application", "Orders", "OrderAppService.cs"), "Shop/Application/Orders/OrderAppService.cs", project, "\n");
            var userFile = new MigrationFile(Path.Combine(project, "Application", "Users", "UserAppService.cs"), "Shop/Application/Users/UserAppService.cs", project, "\n");
            fixer.Scan(orderFile, Lf(orderSource), session);
            fixer.Scan(userFile, Lf(userSource), session);
            fixer.Apply(orderFile, Lf(orderSource), session);
            fixer.Apply(userFile, Lf(userSource), session);
            V4OperationsGenerator.Complete(session, root);

            string Generated(params string[] segments) => Lf(session.GeneratedFiles[Path.Combine([project, "Operations", .. segments])]);

            // 接口类型的返回值没有到 Result<T> 的用户定义转换：显式包 Result.Success
            Assert.Contains("    public async ValueTask<Result<IReadOnlyList<OrderListDto>>> HandleAsync(GetOrdersRequest request, InvocationContext context)\n        => Result.Success(await service.GetOrdersAsync(context.CancellationToken).ConfigureAwait(false));\n", Generated("Order", "GetOrdersOperation.cs"), StringComparison.Ordinal);
            Assert.Contains("public sealed record DeleteRequest(long Id);", Generated("Order", "DeleteOperation.cs"), StringComparison.Ordinal);
            Assert.Contains("public sealed record DeleteRequest(long Id);", Generated("User", "DeleteOperation.cs"), StringComparison.Ordinal);

            // 序列化上下文：生成的类型写全限定名，同名记录加 TypeInfoPropertyName，不加资源子命名空间 using
            var context = Generated("ShopOperationsJsonContext.cs");
            Assert.DoesNotContain("using Shop.Operations.Order;", context, StringComparison.Ordinal);
            Assert.DoesNotContain("using Shop.Operations.User;", context, StringComparison.Ordinal);
            Assert.Contains("using Shop.Application.Dtos;\n", context, StringComparison.Ordinal);
            Assert.Contains("[JsonSerializable(typeof(Shop.Operations.Order.DeleteRequest), TypeInfoPropertyName = \"OrderDeleteRequest\")]\n", context, StringComparison.Ordinal);
            Assert.Contains("[JsonSerializable(typeof(Shop.Operations.User.DeleteRequest), TypeInfoPropertyName = \"UserDeleteRequest\")]\n", context, StringComparison.Ordinal);
            Assert.Contains("[JsonSerializable(typeof(Shop.Operations.Order.GetOrdersRequest))]\n", context, StringComparison.Ordinal);
            Assert.Contains("[JsonSerializable(typeof(Shop.Operations.EmptyResponse))]\n", context, StringComparison.Ordinal);
            Assert.Contains("[JsonSerializable(typeof(IReadOnlyList<OrderListDto>))]\n", context, StringComparison.Ordinal);
            Assert.DoesNotContain("TypeInfoPropertyName = \"OrderGetOrdersRequest\"", context, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LocalEvent_RenamesAggregateEventCalls_InAnyFile()
    {
        const string source = """
            namespace Shop.Entities;

            public sealed partial class Order
            {
                public void Close(string reason)
                {
                    AddLocalEvent(new OrderClosedDomainEvent(Id, reason));
                    this.AddDistributedEvent(new OrderClosedDomainEvent(Id, reason));
                }
            }

            """;
        const string expected = """
            namespace Shop.Entities;

            public sealed partial class Order
            {
                public void Close(string reason)
                {
                    Raise(new OrderClosedDomainEvent(Id, reason));
                    this.Raise(new OrderClosedDomainEvent(Id, reason));
                }
            }

            """;

        var (text, session) = Apply(new LocalEventFixer(), source);

        // partial 类的另一个文件里也改；只改名，不补消息相关的 using，不留标记
        Assert.Equal(Lf(expected), text);
        Assert.Equal(2, session.Findings.Count);
        Assert.DoesNotContain(session.Findings, static finding => finding.IsMarker);

        var (again, second) = Apply(new LocalEventFixer(), text);
        Assert.Equal(text, again);
        Assert.Empty(second.Findings);
    }

    [Fact]
    public void LocalizationResource_MergesPerCulture_DeletesSources_AndEmbeds()
    {
        var root = Path.Combine(Path.GetTempPath(), "xihan-localization-" + Guid.NewGuid().ToString("N"));
        var project = Path.Combine(root, "Shop.Web");
        var localization = Path.Combine(project, "Localization");
        Directory.CreateDirectory(localization);
        try
        {
            File.WriteAllText(Path.Combine(project, "Shop.Web.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <OutputType>Exe</OutputType>\n  </PropertyGroup>\n</Project>\n");
            File.WriteAllText(Path.Combine(localization, "ApiResponse.en-US.json"), "{\n  \"resource\": \"ApiResponse\",\n  \"culture\": \"en-US\",\n  \"texts\": {\n    \"NotModified\": \"Not modified\",\n    \"Created\": \"Created {0}\"\n  }\n}\n");
            File.WriteAllText(Path.Combine(localization, "ApiResponse.zh-CN.json"), "{ \"resource\": \"ApiResponse\", \"culture\": \"zh-CN\", \"texts\": { \"NotModified\": \"未修改\", \"Created\": \"已创建 {0}\" } }");
            File.WriteAllText(Path.Combine(localization, "Validation.en-US.json"), "{ \"resource\": \"Validation\", \"culture\": \"en-US\", \"texts\": { \"Required\": \"{0} is required\" } }");

            var first = new V4Migration(root, [new LocalizationResourceFixer()], dryRun: false).Run(CancellationToken.None);

            Assert.Equal("{\n  \"web.apiresponse.created\": \"Created {0}\",\n  \"web.apiresponse.not_modified\": \"Not modified\",\n  \"web.validation.required\": \"{0} is required\"\n}\n", Lf(File.ReadAllText(Path.Combine(localization, "en-US.json"))));
            Assert.Equal("{\n  \"web.apiresponse.created\": \"已创建 {0}\",\n  \"web.apiresponse.not_modified\": \"未修改\"\n}\n", Lf(File.ReadAllText(Path.Combine(localization, "zh-CN.json"))));
            Assert.Equal(["en-US.json", "zh-CN.json"], Directory.GetFiles(localization).Select(Path.GetFileName).Order(StringComparer.Ordinal));
            var csproj = File.ReadAllText(Path.Combine(project, "Shop.Web.csproj"));
            Assert.Contains("<EmbeddedResource Include=\"Localization/*.json\" LogicalName=\"Localization/%(Filename)%(Extension)\" />", csproj, StringComparison.Ordinal);
            Assert.Contains("<!-- " + Markers.Text("V4-09", CliMessages.MigrateMarkerLocalizationCatalog.L(("domain", "web"))) + " -->", csproj, StringComparison.Ordinal);
            var rule = Assert.Single(first.Rules);
            Assert.Equal(("V4-09", 6, 1), (rule.Id, rule.Changed, rule.Markers));

            var second = new V4Migration(root, [new LocalizationResourceFixer()], dryRun: false).Run(CancellationToken.None);
            Assert.All(second.Rules, static rule => Assert.Equal((0, 0), (rule.Changed, rule.Markers)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("NotModified", "not_modified")]
    [InlineData("HTTPError", "httperror")]
    [InlineData("Key With Spaces", "key_with_spaces")]
    [InlineData("404NotFound", "k_404_not_found")]
    [InlineData("", "text")]
    public void Snake_FollowsKeyGrammar(string key, string expected) => Assert.Equal(expected, V4LocalizationGenerator.Snake(key));

    /// <summary>
    /// 临时目录下的一个空项目（只有 .csproj），给需要项目目录的规则用。
    /// </summary>
    private sealed class TempProject : IDisposable
    {
        /// <summary>
        /// 临时根目录。
        /// </summary>
        private readonly string _root = Path.Combine(Path.GetTempPath(), "xihan-project-" + Guid.NewGuid().ToString("N"));

        /// <summary>
        /// 项目目录。
        /// </summary>
        public string Directory { get; }

        /// <summary>
        /// 创建 <c>&lt;临时根&gt;/&lt;name&gt;/&lt;name&gt;.csproj</c>。
        /// </summary>
        public TempProject(string name)
        {
            Directory = Path.Combine(_root, name);
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(Path.Combine(Directory, name + ".csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        }

        /// <inheritdoc />
        public void Dispose() => System.IO.Directory.Delete(_root, recursive: true);
    }

    [Theory]
    [InlineData("CreateAccessPolicyAsync", "AccessPolicy", "create")]
    [InlineData("UpdateAccessPolicyStatusAsync", "AccessPolicy", "update-status")]
    [InlineData("GetAccessPolicyPageAsync", "AccessPolicy", "list")]
    [InlineData("GetAsync", "User", "get")]
    [InlineData("GetUserListAsync", "User", "list")]
    [InlineData("GetMenuTreeAsync", "Role", "get-menu-tree")]
    [InlineData("QueryAsync", "Log", "list")]
    [InlineData("RemoveAsync", "Log", "delete")]
    [InlineData("ResetPasswordAsync", "User", "reset-password")]
    [InlineData("Export", "User", "export")]
    [InlineData("CreateOAuthBindTicketAsync", "Auth", "create-o-auth-bind-ticket")]
    [InlineData("UpdateOAuthAppStatusAsync", "OAuthApp", "update-status")]
    [InlineData("RevokeAuthTokenAsync", "Auth", "revoke-token")]
    public void Verb_FollowsPrefixTable(string method, string resource, string expected)
        => Assert.Equal(expected, V4Operations.Verb(method, V4Operations.Words(resource)));

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
    public void UnitOfWork_SingletonConsumers_InjectGeneratedCurrentUnitOfWorkAccessor()
    {
        var root = Path.Combine(Path.GetTempPath(), "xihan-uow-" + Guid.NewGuid().ToString("N"));
        var core = Path.Combine(root, "Shop.Core");
        var orders = Path.Combine(root, "Shop.Orders");
        Directory.CreateDirectory(Path.Combine(core, "Data"));
        Directory.CreateDirectory(Path.Combine(core, "Caching"));
        Directory.CreateDirectory(Path.Combine(orders, "Messaging"));
        try
        {
            File.WriteAllText(Path.Combine(core, "Shop.Core.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
            File.WriteAllText(Path.Combine(orders, "Shop.Orders.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <ItemGroup>\n    <ProjectReference Include=\"..\\Shop.Core\\Shop.Core.csproj\" />\n  </ItemGroup>\n</Project>\n");
            // 应用已承接的 4.x 工作单元管理器接口：生成的访问器直接用它的命名空间
            File.WriteAllText(Path.Combine(core, "Data", "IUnitOfWorkManager.cs"), "namespace Shop.Core.Data;\n\npublic interface IUnitOfWorkManager\n{\n    IUnitOfWork? Current { get; }\n}\n");
            File.WriteAllText(Path.Combine(core, "Caching", "CoreCache.cs"), "using Microsoft.Extensions.DependencyInjection;\nusing XiHan.Framework.Uow;\n\nnamespace Shop.Core.Caching;\n\npublic sealed class CoreCache(IUnitOfWorkManager unitOfWorkManager)\n{\n    public bool Pending => unitOfWorkManager.Current is not null;\n}\n\npublic static class CoreServiceCollectionExtensions\n{\n    public static IServiceCollection AddCoreCache(this IServiceCollection services) => services.AddSingleton<CoreCache>();\n}\n");
            File.WriteAllText(Path.Combine(orders, "Messaging", "Outbox.cs"), """
                using Microsoft.Extensions.DependencyInjection;
                using Microsoft.Extensions.DependencyInjection.Extensions;
                using XiHan.Framework.Uow;

                namespace Shop.Orders.Messaging;

                public sealed class Outbox
                {
                    private readonly XiHan.Framework.Uow.IUnitOfWorkManager _unitOfWorkManager;

                    public Outbox(IUnitOfWorkManager unitOfWorkManager)
                    {
                        _unitOfWorkManager = unitOfWorkManager;
                    }

                    public void Enqueue() => _ = _unitOfWorkManager.Current;
                }

                public sealed class Sweeper(IUnitOfWorkManager unitOfWorkManager) : IBackgroundWorker
                {
                    public void Sweep() => _ = unitOfWorkManager.Current;
                }

                public sealed class Rebuilder(IUnitOfWorkManager unitOfWorkManager) : IBackgroundWorker
                {
                    public void Rebuild()
                    {
                        using var uow = unitOfWorkManager.Begin();
                    }
                }

                [UnitOfWork]
                public sealed class OrderService(IUnitOfWorkManager unitOfWorkManager)
                {
                    public void Place() => _ = unitOfWorkManager.Current;
                }

                public static class OrdersServiceCollectionExtensions
                {
                    public static IServiceCollection AddOrders(this IServiceCollection services)
                    {
                        services.TryAddSingleton<Outbox>();
                        services.AddScoped<OrderService>();
                        return services;
                    }
                }

                """);
            var unitOfWorkManager = Markers.Line("V4-05", CliMessages.MigrateMarkerUnitOfWorkManager.L());
            var expectedOutbox = $$"""
                using Microsoft.Extensions.DependencyInjection;
                using Microsoft.Extensions.DependencyInjection.Extensions;
                using Shop.Core.Data;
                using XiHan.Framework.Uow;

                namespace Shop.Orders.Messaging;

                public sealed class Outbox
                {
                    private readonly ICurrentUnitOfWorkAccessor _unitOfWorkManager;

                    public Outbox(ICurrentUnitOfWorkAccessor unitOfWorkManager)
                    {
                        _unitOfWorkManager = unitOfWorkManager;
                    }

                    public void Enqueue() => _ = _unitOfWorkManager.Current;
                }

                public sealed class Sweeper(ICurrentUnitOfWorkAccessor unitOfWorkManager) : IBackgroundWorker
                {
                    public void Sweep() => _ = unitOfWorkManager.Current;
                }

                public sealed class Rebuilder(IUnitOfWorkManager unitOfWorkManager) : IBackgroundWorker
                {
                    public void Rebuild()
                    {
                        {{unitOfWorkManager}}
                        using var uow = unitOfWorkManager.Begin();
                    }
                }

                public sealed class OrderService(IUnitOfWorkManager unitOfWorkManager)
                {
                    public void Place() => _ = unitOfWorkManager.Current;
                }

                public static class OrdersServiceCollectionExtensions
                {
                    public static IServiceCollection AddOrders(this IServiceCollection services)
                    {
                        services.TryAddSingleton<Outbox>();
                        services.AddScoped<OrderService>();
                        return services;
                    }
                }

                """;

            var first = new V4Migration(root, [new DependencyMarkerFixer(), new UnitOfWorkFixer()], dryRun: false).Run(CancellationToken.None);

            // 只读 Current 的单例消费方（显式 TryAddSingleton / AddSingleton 或经 IBackgroundWorker 继承单例存期）改注入访问器并补 using；调用 Begin 的单例与非单例消费方维持原样（Begin 留标记）
            Assert.Equal(Lf(expectedOutbox), Lf(File.ReadAllText(Path.Combine(orders, "Messaging", "Outbox.cs"))));
            var coreCache = Lf(File.ReadAllText(Path.Combine(core, "Caching", "CoreCache.cs")));
            Assert.Contains("using Shop.Core.Data;\n", coreCache, StringComparison.Ordinal);
            Assert.Contains("public sealed class CoreCache(ICurrentUnitOfWorkAccessor unitOfWorkManager)", coreCache, StringComparison.Ordinal);
            // 访问器生成在消费项目引用链最底层的 Shop.Core 的 Data 目录，与承接的 IUnitOfWorkManager 同命名空间
            Assert.False(Directory.Exists(Path.Combine(orders, "Data")));
            var accessorMarker = Markers.Line("V4-05", CliMessages.MigrateMarkerUnitOfWorkAccessor.L());
            Assert.StartsWith(accessorMarker + "\n", Lf(File.ReadAllText(Path.Combine(core, "Data", "ICurrentUnitOfWorkAccessor.cs"))), StringComparison.Ordinal);
            Assert.Contains("public interface ICurrentUnitOfWorkAccessor\n{\n", Lf(File.ReadAllText(Path.Combine(core, "Data", "ICurrentUnitOfWorkAccessor.cs"))), StringComparison.Ordinal);
            Assert.Contains("    IUnitOfWork? Current { get; }\n", Lf(File.ReadAllText(Path.Combine(core, "Data", "ICurrentUnitOfWorkAccessor.cs"))), StringComparison.Ordinal);
            var accessor = Lf(File.ReadAllText(Path.Combine(core, "Data", "CurrentUnitOfWorkAccessor.cs")));
            Assert.StartsWith(accessorMarker + "\n", accessor, StringComparison.Ordinal);
            Assert.Contains("using Microsoft.Extensions.DependencyInjection;\nusing XiHan.Framework.Kernel.Pipeline;\n\nnamespace Shop.Core.Data;\n", accessor, StringComparison.Ordinal);
            Assert.Contains("public sealed class CurrentUnitOfWorkAccessor(IInvocationAccessor accessor) : ICurrentUnitOfWorkAccessor\n{\n    /// <inheritdoc />\n    public IUnitOfWork? Current => accessor.Current.TryGetValue(out var context) ? context.Services.GetService<IUnitOfWorkManager>()?.Current : null;\n}\n", accessor, StringComparison.Ordinal);
            Assert.Contains("services.AddSingleton<ICurrentUnitOfWorkAccessor, CurrentUnitOfWorkAccessor>();", File.ReadAllText(Path.Combine(core, "CoreV4Services.cs")), StringComparison.Ordinal);
            Assert.Equal(3, first.Markers.Count(static marker => marker.Rule == "V4-05"));
            Assert.Equal(2, first.Markers.Count(marker => marker.Rule == "V4-05" && marker.Message == CliMessages.MigrateMarkerUnitOfWorkAccessor.L()));

            var second = new V4Migration(root, [new DependencyMarkerFixer(), new UnitOfWorkFixer()], dryRun: false).Run(CancellationToken.None);
            Assert.All(second.Rules, static rule => Assert.Equal((0, 0), (rule.Changed, rule.Markers)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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
            using XiHan.Framework.Domain;
            using XiHan.Framework.Domain.Shared;
            using XiHan.Framework.Persistence.SqlSugar;

            namespace Shop;

            """;
        const string expected = """
            // header
            using Shop.Core;
            using XiHan.Framework.Kernel.Hosting;
            using XiHan.Framework.Kernel.Security.Claims;
            using XiHan.Framework.Domain;
            using XiHan.Framework.Persistence.SqlSugar;

            namespace Shop;

            """;

        var (text, session) = Apply(new LegacyUsingFixer(), source);

        Assert.Equal(Lf(expected), text);
        Assert.Equal(3, session.Findings.Count);
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
        var feature = Lf(File.ReadAllText(Path.Combine(fixture.Project, "ShopOrdersFeature.cs")));
        Assert.Contains("public sealed class ShopOrdersFeature : IFeature", feature, StringComparison.Ordinal);
        // 生成的注册方法由同项目特性的 Configure 开头调用；文件第一行就不再留调用提醒
        Assert.Contains("    public void Configure(FeatureContext context)\n    {\n        context.Services.AddOrdersV4Services();\n        context.Services.AddOrders();\n", feature, StringComparison.Ordinal);
        var services = Lf(File.ReadAllText(Path.Combine(fixture.Project, "OrdersV4Services.cs")));
        Assert.StartsWith("// " + CliMessages.MigrateGenerated.L() + "\n", services, StringComparison.Ordinal);
        Assert.Contains("services.AddTransient<IOrderService, OrderService>();", services, StringComparison.Ordinal);
        Assert.Contains("services.AddScoped<OrderAppService>();", services, StringComparison.Ordinal);
        var csproj = File.ReadAllText(Path.Combine(fixture.Project, "Shop.Orders.csproj"));
        Assert.Contains("XiHan.Framework.Kernel\"", csproj, StringComparison.Ordinal);
        Assert.Contains("<PackageReference Include=\"XiHan.Framework.Kernel.Generators\" Version=\"" + FrameworkInfo.Version + "\" PrivateAssets=\"all\" />", csproj, StringComparison.Ordinal);
        Assert.Contains("[Operation(\"order.count\")]", File.ReadAllText(Path.Combine(fixture.Project, "Operations", "Order", "CountOperation.cs")), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(fixture.Project, "Operations", "OrdersOperationsJsonContext.cs")));
        Assert.DoesNotContain("[DynamicApi]", File.ReadAllText(Path.Combine(fixture.Project, "Services", "OrderAppService.cs")), StringComparison.Ordinal);
        Assert.Equal(MigrationFixture.SkippedSource, File.ReadAllText(fixture.Skipped));
        Assert.DoesNotContain(first.Markers, static marker => marker.Rule == "V4-03");
        Assert.All(first.Rules.Where(static rule => rule.Id is "V4-01" or "V4-03" or "V4-06" or "V4-10"), static rule => Assert.True(rule.Changed > 0, rule.Id));

        // 再跑一遍：零改动、零标记
        var second = new V4Migration(fixture.Root, V4Rules.All, dryRun: false).Run(CancellationToken.None);
        Assert.All(second.Rules, static rule => Assert.Equal((0, 0), (rule.Changed, rule.Markers)));
        Assert.Empty(second.Markers);
    }

    [Fact]
    public void ModuleToFeature_AddsServicesCallToExistingFeatureConfigure_Once()
    {
        const string feature = """
            using Microsoft.Extensions.DependencyInjection;
            using XiHan.Framework.Kernel;

            namespace Shop.Hosting;

            public sealed class OrdersFeature : IFeature
            {
                public void Configure(FeatureContext context)
                {
                    var services = context.Services;
                    services.AddOrders();
                }
            }

            """;
        const string expected = """
            using Microsoft.Extensions.DependencyInjection;
            using Shop.Orders;
            using XiHan.Framework.Kernel;

            namespace Shop.Hosting;

            public sealed class OrdersFeature : IFeature
            {
                public void Configure(FeatureContext context)
                {
                    context.Services.AddOrdersV4Services();
                    var services = context.Services;
                    services.AddOrders();
                }
            }

            """;

        // 已经是特性的类（再跑一遍或手工迁移过）也插；命名空间不在项目之下时补 using
        Assert.True(ModuleToFeatureFixer.TryAddServicesCall(Lf(feature), "OrdersFeature.cs", "Shop.Orders", "OrdersV4Services", "\n", out var inserted, out var line));
        Assert.Equal(Lf(expected), Lf(inserted));
        Assert.Equal(10, line);

        // 已有调用（不论接收者写法）不重复
        Assert.False(ModuleToFeatureFixer.TryAddServicesCall(inserted, "OrdersFeature.cs", "Shop.Orders", "OrdersV4Services", "\n", out var again, out _));
        Assert.Equal(inserted, again);
        Assert.False(ModuleToFeatureFixer.TryAddServicesCall(Lf(feature).Replace("services.AddOrders();", "services.AddOrdersV4Services();", StringComparison.Ordinal), "OrdersFeature.cs", "Shop.Orders", "OrdersV4Services", "\n", out _, out _));

        // 表达式体改成块
        const string expressionBodied = "namespace Shop.Orders;\n\npublic sealed class OrdersFeature : IHostedFeature\n{\n    public void Configure(FeatureContext context) => context.Services.AddOrders();\n}\n";
        Assert.True(ModuleToFeatureFixer.TryAddServicesCall(expressionBodied, "OrdersFeature.cs", "Shop.Orders", "OrdersV4Services", "\n", out var block, out _));
        Assert.Equal("namespace Shop.Orders;\n\npublic sealed class OrdersFeature : IHostedFeature\n{\n    public void Configure(FeatureContext context)\n    {\n        context.Services.AddOrdersV4Services();\n        context.Services.AddOrders();\n    }\n}\n", Lf(block));
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
