// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Caching.InMemory;

/// <summary>
/// 内存缓存的能力包入口：默认提供 <c>caching.local@1</c> 与 <c>caching.distributed@1</c>。
/// 只为被选中的能力注册服务：另有显式 Provider 提供共享缓存时，本包只注册 <see cref="ICache"/>。
/// 类型化缓存直接存对象，不需要序列化器。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryCachingFeature : IFeature
{
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
            Health = ["caching.inmemory.capacity"],
        },
        Entry = typeof(InMemoryCachingFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddSingleton(InMemoryCachingOptions.FromConfiguration(context.Configuration));
        context.Services.AddSingleton<InMemoryCacheStore>();
        context.Services.AddSingleton<InMemoryObjectCache>();

        if (context.Node.IsSelectedFor(CachingCapabilities.Local))
        {
            context.Services.TryAddSingleton<ICache>(static sp => sp.GetRequiredService<InMemoryObjectCache>());
        }

        if (context.Node.IsSelectedFor(CachingCapabilities.Distributed))
        {
            context.Services.AddSingleton(IdempotencyOptions.FromConfiguration(context.Configuration));
            context.Services.TryAddSingleton<ISharedCache>(static sp => sp.GetRequiredService<InMemoryObjectCache>());
            context.Services.TryAddSingleton<IDistributedLock>(static sp => new StoreDistributedLock(sp.GetRequiredService<InMemoryCacheStore>()));
            context.AddMiddleware<IdempotencyMiddleware>(PipelineStage.Idempotency);
        }
    }
}
