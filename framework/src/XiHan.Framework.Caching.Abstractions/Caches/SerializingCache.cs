// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions.Serialization;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Caches;

/// <summary>
/// 把 <see cref="ICacheStore"/> 与 <see cref="ICacheSerializer"/> 组合成类型化缓存
/// </summary>
/// <remarks>
/// 同时实现 <see cref="ISharedCache"/>，由 Provider 决定注册为哪一个
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class SerializingCache(ICacheStore store, ICacheSerializer serializer) : ISharedCache
{
    /// <summary>
    /// 值与字节之间的序列化器
    /// </summary>
    private readonly ICacheSerializer _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

    /// <summary>
    /// 同键并发回源的在途表
    /// </summary>
    private readonly InFlightLoadTable _loads = new();

    /// <summary>
    /// 在途的键数
    /// </summary>
    /// <remarks>
    /// 测试用它确认回源结束后表里不留条目
    /// </remarks>
    internal int InFlightCount => _loads.Count;

    /// <inheritdoc />
    public ICacheStore Store { get; } = store ?? throw new ArgumentNullException(nameof(store));

    /// <inheritdoc />
    public async ValueTask<Maybe<T>> GetAsync<T>(CacheKey key, CancellationToken cancellationToken)
    {
        var data = await Store.GetAsync(key, cancellationToken).ConfigureAwait(false);
        return data.TryGetValue(out var bytes) ? Maybe.Some(_serializer.Deserialize<T>(bytes)) : default;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Maybe<T>>> GetManyAsync<T>(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var data = await Store.GetManyAsync(keys, cancellationToken).ConfigureAwait(false);
        var results = new Maybe<T>[data.Count];
        for (var index = 0; index < data.Count; index++)
        {
            results[index] = data[index].TryGetValue(out var bytes) ? Maybe.Some(_serializer.Deserialize<T>(bytes)) : default;
        }

        return results;
    }

    /// <inheritdoc />
    public ValueTask SetAsync<T>(CacheKey key, T value, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        var data = _serializer.Serialize(value);
        var validated = options.Validated();
        // 改缓存之前让在途的回源脱钩：它的结果可能早于这次写入，不能在这之后再写回
        _loads.Supersede(key);
        return Store.SetAsync(key, data, validated, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask SetManyAsync<T>(IReadOnlyList<KeyValuePair<CacheKey, T>> entries, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(options);
        // 先把全部值序列化好再一次交给存储：任一值为空在触及存储前就失败
        var validated = options.Validated();
        var encoded = new CacheEntry[entries.Count];
        var keys = new CacheKey[entries.Count];
        for (var index = 0; index < entries.Count; index++)
        {
            ArgumentNullException.ThrowIfNull(entries[index].Value);
            encoded[index] = new CacheEntry(entries[index].Key, _serializer.Serialize(entries[index].Value), validated);
            keys[index] = entries[index].Key;
        }

        _loads.Supersede(keys);
        return Store.SetManyAsync(encoded, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken)
    {
        // 先脱钩再删：删除之后才到的调用不会加入删除之前开始的回源
        _loads.Supersede(key);
        return Store.RemoveAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<bool>> RemoveManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        _loads.Supersede(keys);
        return Store.RemoveManyAsync(keys, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 合并的范围是本实例（ADR-0271）。直接经 <see cref="Store"/> 或别的实例改这个键看不到，在途的回源不会因此脱钩。存储是异步的，脱钩只挡得住还没发出的写回
    /// </remarks>
    public async ValueTask<T> GetOrCreateAsync<T>(CacheKey key, Func<CancellationToken, ValueTask<T>> factory, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(options);
        // 命中是常态：不进在途表，与没有合并时一样只读一次
        var cached = await GetAsync<T>(key, cancellationToken).ConfigureAwait(false);
        if (cached.TryGetValue(out var hit))
        {
            return hit;
        }

        // 回源之前就核对选项：不合法的选项不必先查一次源
        var validated = options.Validated();
        return await _loads.RunAsync(
            key,
            async (load, token) =>
            {
                // 领头的调用再读一次：上面的未命中可能已经过时，上一次回源刚好在那之后写回
                var again = await GetAsync<T>(key, token).ConfigureAwait(false);
                if (again.TryGetValue(out var written))
                {
                    return written;
                }

                var created = await factory(token).ConfigureAwait(false);
                ArgumentNullException.ThrowIfNull(created, nameof(factory));
                // 回源期间这个键被写入或删除过：结果可能早于那次变更，交给等待的调用但不写回
                if (!load.IsSuperseded)
                {
                    // 直接写存储而不经 SetAsync：那是「别人写了这个键」的入口，会让这次回源自己脱钩
                    await Store.SetAsync(key, _serializer.Serialize(created), validated, token).ConfigureAwait(false);
                }

                return created;
            },
            cancellationToken).ConfigureAwait(false);
    }
}
