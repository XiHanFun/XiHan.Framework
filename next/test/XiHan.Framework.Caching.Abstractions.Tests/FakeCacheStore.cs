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
    private readonly ConcurrentDictionary<string, byte[]> _entries = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    public List<string> Operations { get; } = [];

    public int Count => _entries.Count;

    public byte[]? this[string key] => _entries.TryGetValue(key, out var value) ? value : null;

    public ValueTask<Maybe<ReadOnlyMemory<byte>>> GetAsync(CacheKey key, CancellationToken cancellationToken)
    {
        Operations.Add("get:" + key.Value);
        return ValueTask.FromResult(_entries.TryGetValue(key.Value, out var value) ? Maybe.Some(new ReadOnlyMemory<byte>(value)) : default);
    }

    public ValueTask SetAsync(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions options, CancellationToken cancellationToken)
    {
        Operations.Add("set:" + key.Value);
        _entries[key.Value] = value.ToArray();
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> TryAddAsync(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions options, CancellationToken cancellationToken)
    {
        Operations.Add("add:" + key.Value);
        return ValueTask.FromResult(_entries.TryAdd(key.Value, value.ToArray()));
    }

    public ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken)
    {
        Operations.Add("remove:" + key.Value);
        return ValueTask.FromResult(_entries.TryRemove(key.Value, out _));
    }

    public ValueTask<bool> TryRemoveAsync(CacheKey key, ReadOnlyMemory<byte> expectedValue, CancellationToken cancellationToken)
    {
        Operations.Add("cad:" + key.Value);
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
