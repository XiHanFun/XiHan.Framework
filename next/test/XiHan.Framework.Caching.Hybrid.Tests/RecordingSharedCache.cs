// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Caching.InMemory;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Hybrid.Tests;

/// <summary>
/// 记录每次调用的 L2 替身。
/// </summary>
/// <remarks>
/// 转发给内存对象缓存，顺便记下操作名与键，让用例断言哪些读写真的到了 L2。
/// </remarks>
internal sealed class RecordingSharedCache : ISharedCache
{
    /// <summary>
    /// 真正保存数据的内存缓存。
    /// </summary>
    private readonly InMemoryObjectCache _inner;

    /// <summary>
    /// 创建。
    /// </summary>
    public RecordingSharedCache(TimeProvider clock)
    {
        var options = new InMemoryCachingOptions();
        _inner = new InMemoryObjectCache(new InMemoryCacheStore(options, clock), options, clock);
        Store = _inner.Store;
    }

    /// <summary>
    /// 创建，对外的字节存储换成指定的。
    /// </summary>
    /// <remarks>
    /// 数据仍存在内部的内存缓存里；两级缓存只从 <see cref="Store"/> 订阅变更。
    /// </remarks>
    public RecordingSharedCache(TimeProvider clock, ICacheStore store)
        : this(clock)
    {
        Store = store;
    }

    /// <summary>
    /// 到达 L2 的操作，形如 <c>get:k</c>、<c>getmany:a,b</c>。
    /// </summary>
    public List<string> Operations { get; } = [];

    /// <inheritdoc />
    public ICacheStore Store { get; }

    /// <inheritdoc />
    public ValueTask<Maybe<T>> GetAsync<T>(CacheKey key, CancellationToken cancellationToken)
    {
        Operations.Add("get:" + key.Value);
        return _inner.GetAsync<T>(key, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Maybe<T>>> GetManyAsync<T>(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken)
    {
        Operations.Add("getmany:" + string.Join(",", keys.Select(static k => k.Value)));
        return _inner.GetManyAsync<T>(keys, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask SetAsync<T>(CacheKey key, T value, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull
    {
        Operations.Add("set:" + key.Value);
        return _inner.SetAsync(key, value, options, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask SetManyAsync<T>(IReadOnlyList<KeyValuePair<CacheKey, T>> entries, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull
    {
        Operations.Add("setmany:" + string.Join(",", entries.Select(static e => e.Key.Value)));
        return _inner.SetManyAsync(entries, options, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken)
    {
        Operations.Add("remove:" + key.Value);
        return _inner.RemoveAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<bool>> RemoveManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken)
    {
        Operations.Add("removemany:" + string.Join(",", keys.Select(static k => k.Value)));
        return _inner.RemoveManyAsync(keys, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<T> GetOrCreateAsync<T>(CacheKey key, Func<CancellationToken, ValueTask<T>> factory, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull
    {
        Operations.Add("getorcreate:" + key.Value);
        return _inner.GetOrCreateAsync(key, factory, options, cancellationToken);
    }
}
