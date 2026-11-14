// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Testing;

/// <summary>
/// 可以随时切换成「缓存后端不可达」的 <see cref="ICache"/> 包装
/// </summary>
/// <remarks>
/// <see cref="Failing"/> 为真时每个操作都抛出携带 <see cref="CachingErrors.StoreUnavailable"/> 的 <see cref="FrameworkException"/>，与 Redis 断线时一样；为假时原样转给内层缓存。在宿主装配特性之前登记为 <see cref="ICache"/> 单例，用来测试组件在缓存故障时的行为。几个宿主登记同一个实例时，它们共用一份缓存，模拟多实例共用的缓存后端
/// </remarks>
/// <param name="inner">内层缓存</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class FaultyCache(ICache inner) : ICache
{
    /// <summary>
    /// 内层缓存
    /// </summary>
    private readonly ICache _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <summary>
    /// <see cref="Failing"/> 的存储
    /// </summary>
    private volatile bool _failing;

    /// <summary>
    /// 为真时每个操作都失败
    /// </summary>
    public bool Failing
    {
        get => _failing;
        set => _failing = value;
    }

    /// <inheritdoc />
    public ICacheStore Store => _inner.Store;

    /// <inheritdoc />
    public ValueTask<Maybe<T>> GetAsync<T>(CacheKey key, CancellationToken cancellationToken)
    {
        ThrowIfFailing();
        return _inner.GetAsync<T>(key, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Maybe<T>>> GetManyAsync<T>(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken)
    {
        ThrowIfFailing();
        return _inner.GetManyAsync<T>(keys, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask SetAsync<T>(CacheKey key, T value, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull
    {
        ThrowIfFailing();
        return _inner.SetAsync(key, value, options, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask SetManyAsync<T>(IReadOnlyList<KeyValuePair<CacheKey, T>> entries, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull
    {
        ThrowIfFailing();
        return _inner.SetManyAsync(entries, options, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken)
    {
        ThrowIfFailing();
        return _inner.RemoveAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<bool>> RemoveManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken)
    {
        ThrowIfFailing();
        return _inner.RemoveManyAsync(keys, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<T> GetOrCreateAsync<T>(CacheKey key, Func<CancellationToken, ValueTask<T>> factory, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull
    {
        ThrowIfFailing();
        return _inner.GetOrCreateAsync(key, factory, options, cancellationToken);
    }

    /// <summary>
    /// 处于故障状态时抛出
    /// </summary>
    private void ThrowIfFailing()
    {
        if (_failing)
        {
            throw new FrameworkException(CachingErrors.StoreUnavailable, "The cache is unavailable (simulated by FaultyCache).");
        }
    }
}
