// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Caching.Abstractions.Locking;
using XiHan.Framework.Caching.InMemory;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// 有启停的 <c>caching.distributed</c> Provider。
/// </summary>
/// <remarks>
/// 像 Redis 那样启动后才连得上、停止后就连不上：锁存储在 <see cref="StartAsync"/> 之前与 <see cref="StopAsync"/> 之后每个操作都抛存储不可用。依赖它的特性必须排在它之后启动、之前停止。
/// </remarks>
internal sealed class StartStopCachingFeature : IHostedFeature
{
    /// <summary>
    /// 真正的存储。
    /// </summary>
    /// <remarks>
    /// 用例据此查看锁条目是否已删。
    /// </remarks>
    public InMemoryCacheStore Inner { get; } = new(new InMemoryCachingOptions(), TimeProvider.System);

    /// <summary>
    /// 包在外面、按启停开关的存储。
    /// </summary>
    public SwitchableCacheStore Store { get; }

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.tests.startstopcaching",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Provider,
        Provides = [new CapabilityDeclaration(CachingCapabilities.Distributed, CachingCapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Entry = typeof(StartStopCachingFeature).FullName,
    };

    /// <summary>
    /// 创建。
    /// </summary>
    /// <remarks>
    /// 启动之前存储不可用。
    /// </remarks>
    public StartStopCachingFeature() => Store = new SwitchableCacheStore(Inner) { Fail = true };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddSingleton<IDistributedLock>(new StoreDistributedLock(Store));
    }

    /// <inheritdoc />
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        Store.Fail = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        Store.Fail = true;
        return Task.CompletedTask;
    }
}
