// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions.Resolution;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.MultiTenancy.Database.Tenants;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.MultiTenancy.Database;

/// <summary>
/// 数据库租户目录的能力包入口。
/// </summary>
/// <remarks>
/// 提供 <c>multitenancy.context@1</c> 与 <c>multitenancy.sharding@1</c>，需要 <c>persistence.relational@1</c>（启动时核对，见 <see cref="StartAsync"/>）；注册租户目录、解析器（先声明后 Baggage）与 Tenant 阶段中间件。装配后顶替默认的 <c>MultiTenancy.InMemory</c>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DatabaseMultiTenancyFeature : IHostedFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.multitenancy.database",
        Package = "XiHan.Framework.MultiTenancy.Database",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides =
        [
            new CapabilityDeclaration(MultiTenancyCapabilities.Context, MultiTenancyCapabilities.Version),
            new CapabilityDeclaration(MultiTenancyCapabilities.Sharding, MultiTenancyCapabilities.Version),
        ],
        // persistence.relational@1 不写进 requires：持久化 Provider（SqlSugar / EF Core）可选依赖 multitenancy.sharding@1 以按分片选连接，
        // 内核把满足了的可选依赖也当作排序边，这里再声明持久化就成环；实际的依赖改在启动时核对
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions
        {
            Pipeline = [new PipelineContribution(typeof(TenantResolutionMiddleware).FullName!, PipelineStage.Tenant)],
            Config = new ConfigContribution("XiHan:MultiTenancy"),
        },
        Entry = typeof(DatabaseMultiTenancyFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // 本子系统错误码的多语言目录；同名目录只注册一次，多个 Provider 同时装配也不重复
        context.Services.AddMessageCatalog(MultiTenancyMessages.Catalog);
        context.Services.AddSingleton(MultiTenancyOptions.FromConfiguration(context.Configuration));
        context.Services.AddSingleton(DatabaseMultiTenancyOptions.FromConfiguration(context.Configuration));
        context.Services.AddRepository<TenantRecord, string>();
        context.Services.AddSingleton<DatabaseTenantStore>();
        context.Services.TryAddScoped<TenantRecordStore>();
        // 只为被选中的能力注册：另有显式 Provider 提供租户上下文时，本包不登记存储、解析器与中间件（同类型中间件在管道里只能出现一次）
        if (context.Node.IsSelectedFor(MultiTenancyCapabilities.Context))
        {
            context.Services.TryAddSingleton<ITenantStore>(static sp => sp.GetRequiredService<DatabaseTenantStore>());
            // 解析器的注册顺序就是解析顺序：先信已认证主体的声明，再看调用方自报的 Baggage，防止自报租户覆盖身份里的租户
            context.Services.AddSingleton<ITenantResolver, ClaimTenantResolver>();
            context.Services.AddSingleton<ITenantResolver, BaggageTenantResolver>();
            context.AddMiddleware<TenantResolutionMiddleware>(PipelineStage.Tenant);
        }

        // 分片能力被选中时，从租户属性推出分片；应用自己的分片 Provider 顶替本包时这里不注册
        if (context.Node.IsSelectedFor(MultiTenancyCapabilities.Sharding))
        {
            context.Services.TryAddSingleton<ITenantShardResolver, TenantStoreShardResolver>();
        }

        // 管道外的作用域经它显式绑定分片（ADR-0057）；没有分片解析器时按租户绑定成功且不绑定
        context.Services.TryAddScoped(static sp => new TenantShardScope(sp.GetService<ITenantShardResolver>()));
    }

    /// <inheritdoc />
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        // 清单不能声明 persistence.relational@1（见 Manifest 处的说明），改在这里核对图里有它的提供者：
        // 没有持久化 Provider 时启动期就以 KERNEL_CAPABILITY_001 中断，而不是等到第一次解析租户时才发现仓储没有注册
        var graph = services.GetRequiredService<App>().Graph;
        if (!graph.Providers.ContainsKey(PersistenceCapabilities.Relational))
        {
            throw new FrameworkException(
                KernelErrors.MissingCapability,
                $"'{Manifest.Id}' requires '{PersistenceCapabilities.Relational}@{PersistenceCapabilities.Version}', but no manifest provides it.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
