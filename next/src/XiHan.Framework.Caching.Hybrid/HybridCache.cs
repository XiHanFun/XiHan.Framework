// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Hybrid;

/// <summary>
/// 两级缓存。
/// </summary>
/// <remarks>
/// L1 是进程内对象引用（有界 LRU，存活时间不超过 <see cref="HybridCachingOptions.LocalTimeToLive"/>），L2 是被选中的 <see cref="ISharedCache"/>。读：L1 命中直接返回，未命中读 L2 并回填；批量读只把 L1 未命中的键一次交给 L2。写与删：先 L2 再 L1。别的实例的写入与删除不会主动失效本实例的 L1。L1 直接存对象引用，调用方不得修改取回的对象。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class HybridCache : ICache
{
    /// <summary>
    /// L2。
    /// </summary>
    private readonly ISharedCache _shared;

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly HybridCachingOptions _options;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// L1。
    /// </summary>
    private readonly LocalEntryTable _local;

    /// <summary>
    /// 创建。
    /// </summary>
    public HybridCache(ISharedCache shared, HybridCachingOptions options, TimeProvider clock)
    {
        _shared = shared ?? throw new ArgumentNullException(nameof(shared));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _local = new LocalEntryTable(options.LocalCapacity, clock);
    }

    /// <summary>
    /// L2 的字节存储。
    /// </summary>
    public ICacheStore Store => _shared.Store;

    /// <summary>
    /// L1 当前条目数。
    /// </summary>
    public int LocalCount => _local.Count;

    /// <inheritdoc />
    public async ValueTask<Maybe<T>> GetAsync<T>(CacheKey key, CancellationToken cancellationToken)
    {
        // 同一键可能被以别的类型写过：类型不匹配视为未命中，交给 L2 判定
        if (_local.Get(key.Value) is T hit)
        {
            return Maybe.Some(hit);
        }

        var fetched = await _shared.GetAsync<T>(key, cancellationToken).ConfigureAwait(false);
        if (fetched.TryGetValue(out var value))
        {
            _local.Set(key.Value, value!, ExpiresAt(CacheEntryOptions.Default));
        }

        return fetched;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Maybe<T>>> GetManyAsync<T>(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var results = new Maybe<T>[keys.Count];
        var missing = new List<int>();
        for (var index = 0; index < keys.Count; index++)
        {
            if (_local.Get(keys[index].Value) is T hit)
            {
                results[index] = Maybe.Some(hit);
            }
            else
            {
                missing.Add(index);
            }
        }

        if (missing.Count == 0)
        {
            return results;
        }

        // 只把 L1 未命中的键一次交给 L2，命中的回填 L1
        var fetched = await _shared.GetManyAsync<T>([.. missing.Select(index => keys[index])], cancellationToken).ConfigureAwait(false);
        var expiresAt = ExpiresAt(CacheEntryOptions.Default);
        for (var position = 0; position < missing.Count; position++)
        {
            var index = missing[position];
            results[index] = fetched[position];
            if (fetched[position].TryGetValue(out var value))
            {
                _local.Set(keys[index].Value, value!, expiresAt);
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async ValueTask SetAsync<T>(CacheKey key, T value, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        var validated = options.Validated();
        // 先远后近：L2 写失败时 L1 不留下别的实例看不到的值
        await _shared.SetAsync(key, value, validated, cancellationToken).ConfigureAwait(false);
        _local.Set(key.Value, value, ExpiresAt(validated));
    }

    /// <inheritdoc />
    public async ValueTask SetManyAsync<T>(IReadOnlyList<KeyValuePair<CacheKey, T>> entries, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(options);
        var validated = options.Validated();
        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry.Value);
        }

        await _shared.SetManyAsync(entries, validated, cancellationToken).ConfigureAwait(false);
        var expiresAt = ExpiresAt(validated);
        foreach (var entry in entries)
        {
            _local.Set(entry.Key.Value, entry.Value, expiresAt);
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken)
    {
        var removed = await _shared.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        // 两级任一存在过都算删到了东西
        return _local.Remove(key.Value) | removed;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<bool>> RemoveManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var removed = await _shared.RemoveManyAsync(keys, cancellationToken).ConfigureAwait(false);
        var results = new bool[keys.Count];
        for (var index = 0; index < keys.Count; index++)
        {
            results[index] = _local.Remove(keys[index].Value) | removed[index];
        }

        return results;
    }

    /// <inheritdoc />
    public async ValueTask<T> GetOrCreateAsync<T>(CacheKey key, Func<CancellationToken, ValueTask<T>> factory, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(options);
        if (_local.Get(key.Value) is T hit)
        {
            return hit;
        }

        // L2 未命中才执行工厂（由 L2 判定与写入），结果回填 L1
        var validated = options.Validated();
        var value = await _shared.GetOrCreateAsync(key, factory, validated, cancellationToken).ConfigureAwait(false);
        _local.Set(key.Value, value, ExpiresAt(validated));
        return value;
    }

    /// <summary>
    /// L1 的过期时刻。
    /// </summary>
    /// <remarks>
    /// 条目自己的存活或滑动时长与 <see cref="HybridCachingOptions.LocalTimeToLive"/> 的较小者（滑动在 L1 里按绝对时间处理）。
    /// </remarks>
    private DateTimeOffset ExpiresAt(CacheEntryOptions options)
    {
        var limit = _options.LocalTimeToLive;
        var own = options.TimeToLive ?? options.SlidingExpiration;
        var lifetime = own is { } span && span < limit ? span : limit;
        return _clock.GetUtcNow() + lifetime;
    }
}
