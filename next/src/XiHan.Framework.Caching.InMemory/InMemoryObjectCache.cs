// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.InMemory;

/// <summary>
/// 进程内的类型化缓存：直接保存对象引用，不序列化，与 <c>IMemoryCache</c> 同样的约定——调用方不得修改取回的对象。
/// <see cref="Store"/> 指向同一进程的字节存储，供锁与幂等使用。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryObjectCache : ISharedCache
{
    /// <summary>
    /// 直接保存对象引用的有界 LRU 表；与字节存储各自独立计数。
    /// </summary>
    private readonly BoundedLruTable<object> _table;

    /// <summary>
    /// 创建缓存。
    /// </summary>
    public InMemoryObjectCache(InMemoryCacheStore store, InMemoryCachingOptions options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Store = store ?? throw new ArgumentNullException(nameof(store));
        _table = new BoundedLruTable<object>(options.MaxEntries, options.DefaultTimeToLive, timeProvider);
    }

    /// <inheritdoc />
    public ICacheStore Store { get; }

    /// <summary>
    /// 当前对象条目数。
    /// </summary>
    public int Count => _table.Count;

    // 同一键可能被以别的类型写过：类型不匹配视为未命中而不是抛出，与序列化缓存反序列化失败的语义不同
    /// <inheritdoc />
    public ValueTask<Maybe<T>> GetAsync<T>(CacheKey key, CancellationToken cancellationToken)
        => ValueTask.FromResult(_table.Get(key.Value) is T value ? Maybe.Some(value) : default);

    /// <inheritdoc />
    public ValueTask SetAsync<T>(CacheKey key, T value, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        _table.Set(key.Value, value, options.Validated());
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken) => ValueTask.FromResult(_table.Remove(key.Value));

    /// <inheritdoc />
    public async ValueTask<T> GetOrCreateAsync<T>(CacheKey key, Func<CancellationToken, ValueTask<T>> factory, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(options);
        if (_table.Get(key.Value) is T hit)
        {
            return hit;
        }

        var created = await factory(cancellationToken).ConfigureAwait(false);
        _table.Set(key.Value, created, options.Validated());
        return created;
    }
}
