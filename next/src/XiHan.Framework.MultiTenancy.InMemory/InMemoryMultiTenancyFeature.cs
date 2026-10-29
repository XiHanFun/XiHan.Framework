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
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.MultiTenancy.InMemory;

/// <summary>
/// 内存多租户的能力包入口。
/// </summary>
/// <remarks>
/// 默认提供 <c>multitenancy.context@1</c>，注册租户存储、解析器（先声明后 Baggage）与 Tenant 阶段中间件。健康项 <see cref="HealthName"/> 在租户存储满时 Degraded。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryMultiTenancyFeature : IFeature
{
    /// <summary>
    /// 健康项名：租户存储是否还有余量。
    /// </summary>
    public const string HealthName = "multitenancy.inmemory.capacity";

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.multitenancy.inmemory",
        Package = "XiHan.Framework.MultiTenancy.InMemory",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Default,
        Provides =
        [
            new CapabilityDeclaration(MultiTenancyCapabilities.Context, MultiTenancyCapabilities.Version),
            new CapabilityDeclaration(MultiTenancyCapabilities.Sharding, MultiTenancyCapabilities.Version),
        ],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions
        {
            Pipeline = [new PipelineContribution(typeof(TenantResolutionMiddleware).FullName!, PipelineStage.Tenant)],
            Config = new ConfigContribution("XiHan:MultiTenancy"),
            Health = [HealthName],
        },
        Entry = typeof(InMemoryMultiTenancyFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // 本子系统错误码的多语言目录；同名目录只注册一次，多个 Provider 同时装配也不重复
        context.Services.AddMessageCatalog(MultiTenancyMessages.Catalog);
        context.Services.AddSingleton(MultiTenancyOptions.FromConfiguration(context.Configuration));
        context.Services.AddSingleton(InMemoryMultiTenancyOptions.FromConfiguration(context.Configuration));
        context.Services.AddSingleton<InMemoryTenantStore>();
        context.Services.TryAddSingleton<ITenantStore>(static sp => sp.GetRequiredService<InMemoryTenantStore>());
        // 存储自己就是健康项的贡献者：直接看租户数与容量
        context.Services.AddSingleton<IHealthContributor>(static sp => sp.GetRequiredService<InMemoryTenantStore>());
        // 解析器的注册顺序就是解析顺序：先信已认证主体的声明，再看调用方自报的 Baggage，防止自报租户覆盖身份里的租户
        context.Services.AddSingleton<ITenantResolver, ClaimTenantResolver>();
        context.Services.AddSingleton<ITenantResolver, BaggageTenantResolver>();
        context.AddMiddleware<TenantResolutionMiddleware>(PipelineStage.Tenant);
        // 分片能力被选中时，从租户属性推出分片；应用自己的分片 Provider 顶替本包时这里不注册
        if (context.Node.IsSelectedFor(MultiTenancyCapabilities.Sharding))
        {
            context.Services.TryAddSingleton<ITenantShardResolver, TenantStoreShardResolver>();
        }
    }
}
