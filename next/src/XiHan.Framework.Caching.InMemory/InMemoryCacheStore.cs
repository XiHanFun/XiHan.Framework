// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.InMemory;

/// <summary>
/// 有界 LRU 的字节存储。写入复制字节，读取返回不可修改的视图。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryCacheStore : ICacheStore
{
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
    public ValueTask SetAsync(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        _table.Set(key.Value, value.ToArray(), options.Validated());
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
    public ValueTask<bool> TryRemoveAsync(CacheKey key, ReadOnlyMemory<byte> expectedValue, CancellationToken cancellationToken)
        => ValueTask.FromResult(_table.Remove(key.Value, current => expectedValue.Span.SequenceEqual(current)));

    /// <summary>
    /// 清空。
    /// </summary>
    public void Clear() => _table.Clear();
}
