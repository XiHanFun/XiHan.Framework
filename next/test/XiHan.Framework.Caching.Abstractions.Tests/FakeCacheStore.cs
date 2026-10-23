// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Tests;

/// <summary>
/// 无过期的字典存储，只用于验证建立在存储之上的组件。
/// </summary>
internal sealed class FakeCacheStore : ICacheStore
{
    /// <summary>
    /// 键到字节的字典；键区分大小写，没有过期。
    /// </summary>
    private readonly ConcurrentDictionary<string, byte[]> _entries = new(StringComparer.Ordinal);

    /// <summary>
    /// 使比较删的"读取并比较"与"删除"成为一步。
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 按发生顺序记录的操作（<c>get:</c>、<c>set:</c>、<c>add:</c>、<c>remove:</c>、<c>cad:</c> 加键），供测试断言调用序列。
    /// </summary>
    public List<string> Operations { get; } = [];

    /// <summary>
    /// 当前条目数。
    /// </summary>
    public int Count => _entries.Count;

    /// <summary>
    /// 直接按原始键读取字节；不存在为 <see langword="null"/>。
    /// </summary>
    public byte[]? this[string key] => _entries.TryGetValue(key, out var value) ? value : null;

    /// <summary>
    /// 记录 <c>get:</c> 并读取。
    /// </summary>
    public ValueTask<Maybe<ReadOnlyMemory<byte>>> GetAsync(CacheKey key, CancellationToken cancellationToken)
    {
        Operations.Add("get:" + key.Value);
        return ValueTask.FromResult(_entries.TryGetValue(key.Value, out var value) ? Maybe.Some(new ReadOnlyMemory<byte>(value)) : default);
    }

    /// <summary>
    /// 记录 <c>set:</c> 并写入；忽略过期选项。
    /// </summary>
    public ValueTask SetAsync(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions options, CancellationToken cancellationToken)
    {
        Operations.Add("set:" + key.Value);
        _entries[key.Value] = value.ToArray();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 记录 <c>add:</c> 并在不存在时写入。
    /// </summary>
    public ValueTask<bool> TryAddAsync(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions options, CancellationToken cancellationToken)
    {
        Operations.Add("add:" + key.Value);
        return ValueTask.FromResult(_entries.TryAdd(key.Value, value.ToArray()));
    }

    /// <summary>
    /// 记录 <c>remove:</c> 并删除。
    /// </summary>
    public ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken)
    {
        Operations.Add("remove:" + key.Value);
        return ValueTask.FromResult(_entries.TryRemove(key.Value, out _));
    }

    /// <summary>
    /// 记录 <c>getmany:</c> 并逐键读取。
    /// </summary>
    public ValueTask<IReadOnlyList<Maybe<ReadOnlyMemory<byte>>>> GetManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken)
    {
        Operations.Add("getmany:" + string.Join(",", keys.Select(static k => k.Value)));
        var results = new Maybe<ReadOnlyMemory<byte>>[keys.Count];
        for (var index = 0; index < keys.Count; index++)
        {
            results[index] = _entries.TryGetValue(keys[index].Value, out var value) ? Maybe.Some(new ReadOnlyMemory<byte>(value)) : default;
        }

        return ValueTask.FromResult<IReadOnlyList<Maybe<ReadOnlyMemory<byte>>>>(results);
    }

    /// <summary>
    /// 记录 <c>setmany:</c> 并逐项写入；忽略过期选项。
    /// </summary>
    public ValueTask SetManyAsync(IReadOnlyList<CacheEntry> entries, CancellationToken cancellationToken)
    {
        Operations.Add("setmany:" + string.Join(",", entries.Select(static e => e.Key.Value)));
        foreach (var entry in entries)
        {
            _entries[entry.Key.Value] = entry.Value.ToArray();
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 记录 <c>removemany:</c> 并逐键删除。
    /// </summary>
    public ValueTask<IReadOnlyList<bool>> RemoveManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken)
    {
        Operations.Add("removemany:" + string.Join(",", keys.Select(static k => k.Value)));
        var results = new bool[keys.Count];
        for (var index = 0; index < keys.Count; index++)
        {
            results[index] = _entries.TryRemove(keys[index].Value, out _);
        }

        return ValueTask.FromResult<IReadOnlyList<bool>>(results);
    }

    /// <summary>
    /// 记录 <c>cad:</c>，当前值与期望值相同时才删除。
    /// </summary>
    public ValueTask<bool> TryRemoveAsync(CacheKey key, ReadOnlyMemory<byte> expectedValue, CancellationToken cancellationToken)
    {
        Operations.Add("cad:" + key.Value);
        // 并发字典没有"比较值后删除"，用锁把比较与删除合成一步
        lock (_gate)
        {
            if (_entries.TryGetValue(key.Value, out var current) && expectedValue.Span.SequenceEqual(current))
            {
                return ValueTask.FromResult(_entries.TryRemove(key.Value, out _));
            }

            return ValueTask.FromResult(false);
        }
    }
}
