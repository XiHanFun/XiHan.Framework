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
            Assert.Contains("using Shop.Operations.Order;\n", context, StringComparison.Ordinal);
            foreach (var type in new[] { "CountRequest", "EmptyResponse", "GetOrderRequest", "OrderCreateDto", "OrderDetailDto", "OrderPageQueryDto", "OrderStatusUpdateDto", "PagedResult<OrderListDto>", "int" })
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
            Assert.Contains("public string? TenantId => Security.Tenant?.Id ?? Find(TenantIdClaim);", currentUser, StringComparison.Ordinal);
            Assert.Contains("string? UserId { get; }", File.ReadAllText(Path.Combine(core, "Security", "ICurrentUser.cs")), StringComparison.Ordinal);
            Assert.Contains("IDisposable Change(string? id, string? name = null);", File.ReadAllText(Path.Combine(core, "Security", "ICurrentTenant.cs")), StringComparison.Ordinal);
            Assert.Contains("throw new NotSupportedException(", File.ReadAllText(Path.Combine(core, "Security", "CurrentTenant.cs")), StringComparison.Ordinal);

            Assert.StartsWith("using Shop.Core.Security;\nusing XiHan.Framework.Security.Users;\n", Lf(File.ReadAllText(Path.Combine(core, "Services", "Auditor.cs"))), StringComparison.Ordinal);
            var orderService = Lf(File.ReadAllText(Path.Combine(orders, "Services", "OrderService.cs")));
            Assert.StartsWith("using Shop.Core.Security;\nusing XiHan.Framework.MultiTenancy.Abstractions;\n", orderService, StringComparison.Ordinal);
            Assert.Contains($"        {Markers.Line("V4-07", CliMessages.MigrateMarkerTenantChange.L())}\n        using var scope = currentTenant.Change(tenantId);\n", orderService, StringComparison.Ordinal);
            var services = File.ReadAllText(Path.Combine(core, "CoreV4Services.cs"));
            Assert.Contains("services.AddScoped<ICurrentTenant, CurrentTenant>();", services, StringComparison.Ordinal);
            Assert.Contains("services.AddScoped<ICurrentUser, CurrentUser>();", services, StringComparison.Ordinal);
            var rule = Assert.Single(first.Rules);
            Assert.Equal(("V4-07", 1), (rule.Id, rule.Markers));
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
            using XiHan.Framework.Messaging.Abstractions;
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
        Assert.Contains("public sealed class ShopOrdersFeature : IFeature", File.ReadAllText(Path.Combine(fixture.Project, "ShopOrdersFeature.cs")), StringComparison.Ordinal);
        var services = File.ReadAllText(Path.Combine(fixture.Project, "OrdersV4Services.cs"));
        Assert.Contains("services.AddTransient<IOrderService, OrderService>();", services, StringComparison.Ordinal);
        Assert.Contains("services.AddScoped<OrderAppService>();", services, StringComparison.Ordinal);
        var csproj = File.ReadAllText(Path.Combine(fixture.Project, "Shop.Orders.csproj"));
        Assert.Contains("XiHan.Framework.Kernel\"", csproj, StringComparison.Ordinal);
        Assert.Contains("<PackageReference Include=\"XiHan.Framework.Kernel.Generators\" Version=\"" + FrameworkInfo.Version + "\" PrivateAssets=\"all\" />", csproj, StringComparison.Ordinal);
        Assert.Contains("[Operation(\"order.count\")]", File.ReadAllText(Path.Combine(fixture.Project, "Operations", "Order", "CountOperation.cs")), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(fixture.Project, "Operations", "OrdersOperationsJsonContext.cs")));
        Assert.DoesNotContain("[DynamicApi]", File.ReadAllText(Path.Combine(fixture.Project, "Services", "OrderAppService.cs")), StringComparison.Ordinal);
        Assert.Equal(MigrationFixture.SkippedSource, File.ReadAllText(fixture.Skipped));
        Assert.Contains(first.Markers, static marker => marker is { Rule: "V4-03", File: "Shop.Orders/OrdersV4Services.cs", Line: 1 });
        Assert.All(first.Rules.Where(static rule => rule.Id is "V4-01" or "V4-03" or "V4-06" or "V4-10"), static rule => Assert.True(rule.Changed > 0, rule.Id));

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
