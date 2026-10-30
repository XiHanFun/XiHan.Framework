// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.InMemory;

/// <summary>
/// 进程内的类型化缓存。
/// </summary>
/// <remarks>
/// 直接保存对象引用，不序列化，与 <c>IMemoryCache</c> 同样的约定——调用方不得修改取回的对象。<see cref="Store"/> 指向同一进程的字节存储，供锁与幂等使用。批量方法一次加锁逐项处理。对象条目不在字节存储里，经 <see cref="ICacheInspector"/> 列出与查看（ADR-0058）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryObjectCache : ISharedCache, ICacheInspector
{
    /// <summary>
    /// 直接保存对象引用的有界 LRU 表。
    /// </summary>
    /// <remarks>
    /// 与字节存储各自独立计数。
    /// </remarks>
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
    public ValueTask<IReadOnlyList<Maybe<T>>> GetManyAsync<T>(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var values = _table.GetMany(Names(keys));
        var results = new Maybe<T>[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            results[index] = values[index] is T value ? Maybe.Some(value) : default;
        }

        return ValueTask.FromResult<IReadOnlyList<Maybe<T>>>(results);
    }

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
    public ValueTask SetManyAsync<T>(IReadOnlyList<KeyValuePair<CacheKey, T>> entries, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(options);
        var validated = options.Validated();
        var items = new (string Key, object Value, CacheEntryOptions Options)[entries.Count];
        for (var index = 0; index < entries.Count; index++)
        {
            ArgumentNullException.ThrowIfNull(entries[index].Value);
            items[index] = (entries[index].Key.Value, entries[index].Value, validated);
        }

        _table.SetMany(items);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 按键的序数顺序分页，续页标记是上一页最后一个键。
    /// </remarks>
    public ValueTask<CacheKeyPage> ListKeysAsync(CacheKeyQuery query, CancellationToken cancellationToken) => ValueTask.FromResult(_table.ListKeys(query));

    /// <inheritdoc />
    public ValueTask<Maybe<CachedObjectInfo>> InspectAsync(CacheKey key, CancellationToken cancellationToken)
        => ValueTask.FromResult(_table.Peek(key.Value) is { } entry
            ? Maybe.Some(new CachedObjectInfo(key, entry.Value.GetType().FullName ?? entry.Value.GetType().Name, entry.TimeToLive, entry.Sliding))
            : Maybe.None<CachedObjectInfo>());

    /// <inheritdoc />
    public ValueTask<long> RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken) => ValueTask.FromResult(_table.RemoveByPrefix(prefix));

    /// <inheritdoc />
    public ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken) => ValueTask.FromResult(_table.Remove(key.Value));

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<bool>> RemoveManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return ValueTask.FromResult<IReadOnlyList<bool>>(_table.RemoveMany(Names(keys)));
    }

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

    /// <summary>
    /// 取键的字符串形式。
    /// </summary>
    private static string[] Names(IReadOnlyList<CacheKey> keys)
    {
        var names = new string[keys.Count];
        for (var index = 0; index < keys.Count; index++)
        {
            names[index] = keys[index].Value;
        }

        return names;
    }
}
