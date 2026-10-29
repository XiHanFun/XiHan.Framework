// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.InMemory;

/// <summary>
/// 健康项 <see cref="InMemoryCachingFeature.HealthName"/>。
/// </summary>
/// <remarks>
/// 两张 LRU 表（字节存储、对象缓存）任一条目数达到 <see cref="InMemoryCachingOptions.MaxEntries"/> 即 Degraded——缓存仍可用，但每次写入都在淘汰，命中率已受容量限制。同步计算，不起后台循环。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryCacheHealthContributor : IHealthContributor
{
    /// <summary>
    /// 字节存储。
    /// </summary>
    private readonly InMemoryCacheStore _store;

    /// <summary>
    /// 对象缓存。
    /// </summary>
    private readonly InMemoryObjectCache _cache;

    /// <summary>
    /// 每张表的条目上限。
    /// </summary>
    private readonly int _capacity;

    /// <summary>
    /// 创建。
    /// </summary>
    public InMemoryCacheHealthContributor(InMemoryCacheStore store, InMemoryObjectCache cache, InMemoryCachingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _capacity = options.MaxEntries;
    }

    /// <inheritdoc />
    public IReadOnlyList<ContributorStatus> Check()
    {
        // 用量达到容量即 Degraded：LRU 不拒绝写入，所以这是唯一能看出"缓存太小"的信号
        var full = _store.Count >= _capacity || _cache.Count >= _capacity;
        return
        [
            full
                ? new ContributorStatus(InMemoryCachingFeature.HealthName, LifecycleState.Degraded, Error.Unavailable(CachingErrors.CapacityReached, CachingMessages.CapacityReached, MessageArguments.Of(("capacity", _capacity))))
                : new ContributorStatus(InMemoryCachingFeature.HealthName, LifecycleState.Ready),
        ];
    }
}
