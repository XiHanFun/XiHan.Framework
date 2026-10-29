// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.InMemory;

/// <summary>
/// 有界 LRU 的字节存储。
/// </summary>
/// <remarks>
/// 写入复制字节，读取返回不可修改的视图；批量方法一次加锁逐项处理。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryCacheStore : ICacheStore
{
    /// <summary>
    /// 承载字节条目的有界 LRU 表。
    /// </summary>
    private readonly BoundedLruTable<byte[]> _table;

    /// <summary>
    /// 创建存储。
    /// </summary>
    public InMemoryCacheStore(InMemoryCachingOptions options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _table = new BoundedLruTable<byte[]>(options.MaxEntries, options.DefaultTimeToLive, timeProvider);
    }

    /// <summary>
    /// 当前条目数（含尚未清理的过期条目）。
    /// </summary>
    public int Count => _table.Count;

    /// <inheritdoc />
    public ValueTask<Maybe<ReadOnlyMemory<byte>>> GetAsync(CacheKey key, CancellationToken cancellationToken)
        => ValueTask.FromResult(_table.Get(key.Value) is { } value ? Maybe.Some(new ReadOnlyMemory<byte>(value)) : default);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Maybe<ReadOnlyMemory<byte>>>> GetManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var values = _table.GetMany(Names(keys));
        var results = new Maybe<ReadOnlyMemory<byte>>[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            results[index] = values[index] is { } value ? Maybe.Some(new ReadOnlyMemory<byte>(value)) : default;
        }

        return ValueTask.FromResult<IReadOnlyList<Maybe<ReadOnlyMemory<byte>>>>(results);
    }

    /// <inheritdoc />
    public ValueTask SetAsync(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        // 复制一份再存：调用方之后修改自己的缓冲区不会影响缓存内容
        _table.Set(key.Value, value.ToArray(), options.Validated());
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask SetManyAsync(IReadOnlyList<CacheEntry> entries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        // 进锁前先校验全部选项并复制字节：任一项不合法整批不写
        var items = new (string Key, byte[] Value, CacheEntryOptions Options)[entries.Count];
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            ArgumentNullException.ThrowIfNull(entry);
            items[index] = (entry.Key.Value, entry.Value.ToArray(), entry.Options.Validated());
        }

        _table.SetMany(items);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<bool> TryAddAsync(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return ValueTask.FromResult(_table.TryAdd(key.Value, value.ToArray(), options.Validated()));
    }

    /// <inheritdoc />
    public ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken)
        => ValueTask.FromResult(_table.Remove(key.Value));

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<bool>> RemoveManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return ValueTask.FromResult<IReadOnlyList<bool>>(_table.RemoveMany(Names(keys)));
    }

    /// <inheritdoc />
    public ValueTask<bool> TryRemoveAsync(CacheKey key, ReadOnlyMemory<byte> expectedValue, CancellationToken cancellationToken)
        => ValueTask.FromResult(_table.Remove(key.Value, current => expectedValue.Span.SequenceEqual(current)));

    /// <summary>
    /// 清空。
    /// </summary>
    public void Clear() => _table.Clear();

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
