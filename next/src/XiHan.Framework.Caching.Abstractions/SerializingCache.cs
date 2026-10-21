// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions;

/// <summary>
/// 把 <see cref="ICacheStore"/> 与 <see cref="ICacheSerializer"/> 组合成类型化缓存。同时实现 <see cref="ISharedCache"/>，由 Provider 决定注册为哪一个。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class SerializingCache(ICacheStore store, ICacheSerializer serializer) : ISharedCache
{
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
    public ValueTask SetAsync<T>(CacheKey key, T value, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        return Store.SetAsync(key, _serializer.Serialize(value), options.Validated(), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken) => Store.RemoveAsync(key, cancellationToken);

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

        var created = await factory(cancellationToken).ConfigureAwait(false);
        await SetAsync(key, created, options, cancellationToken).ConfigureAwait(false);
        return created;
    }
}
