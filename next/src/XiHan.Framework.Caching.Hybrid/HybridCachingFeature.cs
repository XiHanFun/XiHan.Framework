// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Caching.Hybrid;

/// <summary>
/// 两级缓存的能力包入口：显式 Provider，提供 <c>caching.local@1</c>（顶替内存实现的进程内缓存），依赖 <c>caching.distributed@1</c> 作为 L2。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class HybridCachingFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.caching.hybrid",
        Package = "XiHan.Framework.Caching.Hybrid",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides = [new CapabilityDeclaration(CachingCapabilities.Local, CachingCapabilities.Version)],
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(CachingCapabilities.Distributed, CachingCapabilities.Version),
        ],
        Contributes = new Contributions
        {
            Config = new ConfigContribution("XiHan:Caching"),
        },
        Entry = typeof(HybridCachingFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // 本子系统错误码的多语言目录；同名目录只注册一次，多个 Provider 同时装配也不重复
        context.Services.AddMessageCatalog(CachingMessages.Catalog);
        context.Services.AddSingleton(HybridCachingOptions.FromConfiguration(context.Configuration));
        // L2 取被选中提供 caching.distributed 的 ISharedCache；没有登记时钟的宿主用系统时钟
        context.Services.AddSingleton(static sp => new HybridCache(sp.GetRequiredService<ISharedCache>(), sp.GetRequiredService<HybridCachingOptions>(), sp.GetService<TimeProvider>() ?? TimeProvider.System));
        if (context.Node.IsSelectedFor(CachingCapabilities.Local))
        {
            context.Services.TryAddSingleton<ICache>(static sp => sp.GetRequiredService<HybridCache>());
        }
    }
}
