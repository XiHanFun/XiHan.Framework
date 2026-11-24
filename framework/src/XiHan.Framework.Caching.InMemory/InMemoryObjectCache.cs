// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.InMemory;

/// <summary>
/// 进程内的类型化缓存
/// </summary>
/// <remarks>
/// 直接保存对象引用，不序列化，与 <c>IMemoryCache</c> 同样的约定——调用方不得修改取回的对象。<see cref="Store"/> 指向同一进程的字节存储，供锁与幂等使用。批量方法一次加锁逐项处理。对象条目不在字节存储里，经 <see cref="ICacheInspector"/> 列出与查看（ADR-0058）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryObjectCache : ISharedCache, ICacheInspector
{
    /// <summary>
    /// 直接保存对象引用的有界 LRU 表
    /// </summary>
    /// <remarks>
    /// 与字节存储各自独立计数
    /// </remarks>
    private readonly BoundedLruTable<object> _table;

    /// <summary>
    /// 同键并发回源的在途表
    /// </summary>
    private readonly InFlightLoadTable _loads = new();

    /// <summary>
    /// 创建缓存
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
    /// 当前对象条目数
    /// </summary>
    public int Count => _table.Count;

    /// <summary>
    /// 在途的键数
    /// </summary>
    /// <remarks>
    /// 测试用它确认回源结束后表里不留条目
    /// </remarks>
    internal int InFlightCount => _loads.Count;

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
        var validated = options.Validated();
        // 改表之前让在途的回源脱钩：它的结果可能早于这次写入，脱钩之后它不会再写回
        _loads.Supersede(key);
        _table.Set(key.Value, value, validated);
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
        var keys = new CacheKey[entries.Count];
        for (var index = 0; index < entries.Count; index++)
        {
            ArgumentNullException.ThrowIfNull(entries[index].Value);
            items[index] = (entries[index].Key.Value, entries[index].Value, validated);
            keys[index] = entries[index].Key;
        }

        _loads.Supersede(keys);
        _table.SetMany(items);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 按键的序数顺序分页，续页标记是上一页最后一个键。每页上限越界返回 <see cref="Abstractions.CachingErrors.ListPageSizeInvalid"/>
    /// </remarks>
    public ValueTask<Result<CacheKeyPage>> ListKeysAsync(CacheKeyQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        // 先核对上限再读表：越界是调用方的输入错误，按校验结果返回，不按上限截断
        var valid = query.Validate(CacheKeyQuery.Revision);
        return ValueTask.FromResult(valid.IsSuccess ? Result.Success(_table.ListKeys(query)) : Result.Failure<CacheKeyPage>(valid.Error!));
    }

    /// <inheritdoc />
    public ValueTask<Maybe<CachedObjectInfo>> InspectAsync(CacheKey key, CancellationToken cancellationToken)
        => ValueTask.FromResult(_table.Peek(key.Value) is { } entry
            ? Maybe.Some(new CachedObjectInfo(key, entry.Value.GetType().FullName ?? entry.Value.GetType().Name, entry.TimeToLive, entry.Sliding))
            : Maybe.None<CachedObjectInfo>());

    /// <inheritdoc />
    public ValueTask<Result<long>> RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken)
    {
        var valid = CacheKeyQuery.ValidateRemovalPrefix(prefix);
        if (valid.IsFailure)
        {
            return ValueTask.FromResult(Result.Failure<long>(valid.Error!));
        }

        _loads.SupersedePrefix(prefix);
        return ValueTask.FromResult(Result.Success(_table.RemoveByPrefix(prefix)));
    }

    /// <inheritdoc />
    public ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken)
    {
        // 先脱钩再删：删除之后才到的调用不会加入删除之前开始的回源，那次回源也不会把结果写回来
        _loads.Supersede(key);
        return ValueTask.FromResult(_table.Remove(key.Value));
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<bool>> RemoveManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        _loads.Supersede(keys);
        return ValueTask.FromResult<IReadOnlyList<bool>>(_table.RemoveMany(Names(keys)));
    }

    /// <inheritdoc />
    /// <remarks>
    /// 合并的范围是本实例（ADR-0271）。写回与本实例对这个键的写入、删除互斥地核对，被取代的回源一定不会写回
    /// </remarks>
    public ValueTask<T> GetOrCreateAsync<T>(CacheKey key, Func<CancellationToken, ValueTask<T>> factory, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(options);
        // 命中是常态：不进在途表
        if (_table.Get(key.Value) is T hit)
        {
            return ValueTask.FromResult(hit);
        }

        // 回源之前就核对选项：不合法的选项不必先查一次源
        var validated = options.Validated();
        return _loads.RunAsync(
            key,
            async (load, token) =>
            {
                // 领头的调用再读一次：上面的未命中可能已经过时，上一次回源刚好在那之后写回
                if (_table.Get(key.Value) is T written)
                {
                    return written;
                }

                var created = await factory(token).ConfigureAwait(false);
                // 回源期间这个键被写入或删除过：结果可能早于那次变更，交给等待的调用但不写回
                load.TryWrite((Table: _table, Key: key.Value, Value: (object)created, Options: validated), static entry => entry.Table.Set(entry.Key, entry.Value, entry.Options));
                return created;
            },
            cancellationToken);
    }

    /// <summary>
    /// 取键的字符串形式
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
