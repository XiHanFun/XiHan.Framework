// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Distributed;
using XiHan.Framework.Caching.Abstractions.Idempotency;
using XiHan.Framework.Caching.Abstractions.Locking;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Caching.InMemory;

/// <summary>
/// 内存缓存的能力包入口。
/// </summary>
/// <remarks>
/// 默认提供 <c>caching.local@1</c> 与 <c>caching.distributed@1</c>。只为被选中的能力注册服务：另有显式 Provider 提供共享缓存时，本包只注册 <see cref="ICache"/>。类型化缓存直接存对象，不需要序列化器。健康项 <see cref="HealthName"/> 在任一张表满时 Degraded。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryCachingFeature : IFeature
{
    /// <summary>
    /// 健康项名：两张 LRU 表是否都还有余量。
    /// </summary>
    public const string HealthName = "caching.inmemory.capacity";

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.caching.inmemory",
        Package = "XiHan.Framework.Caching.InMemory",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Default,
        Provides =
        [
            new CapabilityDeclaration(CachingCapabilities.Local, CachingCapabilities.Version),
            new CapabilityDeclaration(CachingCapabilities.Distributed, CachingCapabilities.Version),
        ],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions
        {
            Pipeline = [new PipelineContribution(typeof(IdempotencyMiddleware).FullName!, PipelineStage.Idempotency)],
            Config = new ConfigContribution("XiHan:Caching"),
            Health = [HealthName],
        },
        Entry = typeof(InMemoryCachingFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // 本子系统错误码的多语言目录；同名目录只注册一次，多个 Provider 同时装配也不重复
        context.Services.AddMessageCatalog(CachingMessages.Catalog);
        // 具体类型总是注册，供测试与其他组件按类型取用；契约接口只在本包被选中提供对应能力时才绑定
        context.Services.AddSingleton(InMemoryCachingOptions.FromConfiguration(context.Configuration));
        context.Services.AddSingleton<InMemoryCacheStore>();
        context.Services.AddSingleton<InMemoryObjectCache>();
        // 健康项看两张表的用量，与哪些能力被选中无关
        context.Services.AddSingleton<IHealthContributor, InMemoryCacheHealthContributor>();

        if (context.Node.IsSelectedFor(CachingCapabilities.Local))
        {
            context.Services.TryAddSingleton<ICache>(static sp => sp.GetRequiredService<InMemoryObjectCache>());
        }

        // 另有显式 Provider（如 Redis）赢得 caching.distributed 时，这里整段跳过，避免注册重复的锁与幂等中间件
        if (context.Node.IsSelectedFor(CachingCapabilities.Distributed))
        {
            context.Services.AddSingleton(IdempotencyOptions.FromConfiguration(context.Configuration));
            context.Services.TryAddSingleton<ISharedCache>(static sp => sp.GetRequiredService<InMemoryObjectCache>());
            // 被选中的共享存储也以字节级契约登记：应用在"不存在才写 + 比较删"之上搭自己的租约、续约等机制时按接口取用
            context.Services.TryAddSingleton<ICacheStore>(static sp => sp.GetRequiredService<InMemoryCacheStore>());
            context.Services.TryAddSingleton<IDistributedLock>(static sp => new StoreDistributedLock(sp.GetRequiredService<InMemoryCacheStore>()));
            context.Services.AddXiHanDistributedCache(static sp => sp.GetRequiredService<InMemoryCacheStore>());
            context.AddMiddleware<IdempotencyMiddleware>(PipelineStage.Idempotency);
        }
    }
}
