// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Idempotency;
using XiHan.Framework.Caching.Abstractions.Locking;
using XiHan.Framework.Caching.Abstractions.Serialization;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Caching.Remote;

/// <summary>
/// 远程缓存 Provider 的能力包入口（RFC-0019）：提供 <c>caching.distributed@1</c>，存储由实现了操作集 <c>cache-entries.*</c> 的进程外插件承担；
/// 本包注册共享缓存、分布式锁与幂等中间件。插件由 <c>Plugins.OutOfProcess</c> 等插件宿主按目录发现并登记代理操作，本包不启动进程。
/// </summary>
[ApiLevel(Stability.Experimental, "5.0")]
public sealed class RemoteCachingFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.caching.remote",
        Package = "XiHan.Framework.Caching.Remote",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides = [new CapabilityDeclaration(CachingCapabilities.Distributed, CachingCapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions
        {
            Pipeline = [new PipelineContribution(typeof(IdempotencyMiddleware).FullName!, PipelineStage.Idempotency)],
            Config = new ConfigContribution("XiHan:Caching"),
        },
        Entry = typeof(RemoteCachingFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // 子系统目录与插件侧错误的目录；同名目录只注册一次
        context.Services.AddMessageCatalog(CachingMessages.Catalog);
        context.Services.AddMessageCatalog(RemoteCachingMessages.Catalog);
        context.Services.AddSingleton(RemoteCachingOptions.FromConfiguration(context.Configuration));
        context.Services.AddSingleton(IdempotencyOptions.FromConfiguration(context.Configuration));
        context.Services.AddSingleton<RemoteCacheStore>();
        if (!CacheSerializerRegistration.TryAddJsonSerializer(context.Services))
        {
            // 本包不支持 AOT，允许退回反射版序列化器
            context.Services.AddSingleton<ICacheSerializer>(static _ => JsonCacheSerializer.CreateReflectionBased());
        }

        context.Services.TryAddSingleton<ISharedCache>(static sp => new SerializingCache(sp.GetRequiredService<RemoteCacheStore>(), sp.GetRequiredService<ICacheSerializer>()));
        context.Services.TryAddSingleton<IDistributedLock>(static sp => new StoreDistributedLock(sp.GetRequiredService<RemoteCacheStore>()));
        context.AddMiddleware<IdempotencyMiddleware>(PipelineStage.Idempotency);
    }
}
