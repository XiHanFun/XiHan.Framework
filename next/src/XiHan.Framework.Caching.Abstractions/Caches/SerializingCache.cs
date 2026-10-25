// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions.Serialization;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Caches;

/// <summary>
/// 把 <see cref="ICacheStore"/> 与 <see cref="ICacheSerializer"/> 组合成类型化缓存。同时实现 <see cref="ISharedCache"/>，由 Provider 决定注册为哪一个。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class SerializingCache(ICacheStore store, ICacheSerializer serializer) : ISharedCache
{
    /// <summary>
    /// 值与字节之间的序列化器。
    /// </summary>
    private readonly ICacheSerializer _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

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
        return Store.SetAsync(key, _serializer.Serialize(value), options.Validated(), cancellationToken);
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
        for (var index = 0; index < entries.Count; index++)
        {
            ArgumentNullException.ThrowIfNull(entries[index].Value);
            encoded[index] = new CacheEntry(entries[index].Key, _serializer.Serialize(entries[index].Value), validated);
        }

        return Store.SetManyAsync(encoded, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken) => Store.RemoveAsync(key, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<bool>> RemoveManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken) => Store.RemoveManyAsync(keys, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<T> GetOrCreateAsync<T>(CacheKey key, Func<CancellationToken, ValueTask<T>> factory, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(options);
        var cached = await GetAsync<T>(key, cancellationToken).ConfigureAwait(false);
        if (cached.TryGetValue(out var hit))
        {
            return hit;
        }

        // 未命中不做并发合并：多个调用者各自执行工厂并写入，后写覆盖先写
        var created = await factory(cancellationToken).ConfigureAwait(false);
        await SetAsync(key, created, options, cancellationToken).ConfigureAwait(false);
        return created;
    }
}
