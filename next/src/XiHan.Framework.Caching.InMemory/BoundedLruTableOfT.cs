// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions;

namespace XiHan.Framework.Caching.InMemory;

/// <summary>
/// 有界 LRU 表：所有操作在一把锁内完成，过期在访问时清理，满时淘汰最久未访问的条目。
/// </summary>
internal sealed class BoundedLruTable<TValue>(int capacity, TimeSpan defaultTimeToLive, TimeProvider timeProvider)
    where TValue : class
{
    private readonly Dictionary<string, LinkedListNode<Entry>> _index = new(StringComparer.Ordinal);
    private readonly LinkedList<Entry> _recency = new();
    private readonly Lock _gate = new();

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _index.Count;
            }
        }
    }

    public TValue? Get(string key)
    {
        lock (_gate)
        {
            var node = Live(key);
            if (node is null)
            {
                return null;
            }

            if (node.Value.Sliding is { } sliding)
            {
                node.Value.ExpiresAt = timeProvider.GetUtcNow() + sliding;
            }

            _recency.Remove(node);
            _recency.AddFirst(node);
            return node.Value.Value;
        }
    }

    public void Set(string key, TValue value, CacheEntryOptions options)
    {
        lock (_gate)
        {
            Write(key, value, options);
        }
    }

    public bool TryAdd(string key, TValue value, CacheEntryOptions options)
    {
        lock (_gate)
        {
            if (Live(key) is not null)
            {
                return false;
            }

            Write(key, value, options);
            return true;
        }
    }

    public bool Remove(string key, Func<TValue, bool>? condition = null)
    {
        lock (_gate)
        {
            var node = Live(key);
            if (node is null || (condition is not null && !condition(node.Value.Value)))
            {
                return false;
            }

            Evict(node);
            return true;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _index.Clear();
            _recency.Clear();
        }
    }

    private LinkedListNode<Entry>? Live(string key)
    {
        if (!_index.TryGetValue(key, out var node))
        {
            return null;
        }

        if (node.Value.ExpiresAt > timeProvider.GetUtcNow())
        {
            return node;
        }

        Evict(node);
        return null;
    }

    private void Write(string key, TValue value, CacheEntryOptions options)
    {
        if (_index.TryGetValue(key, out var existing))
        {
            Evict(existing);
        }

        while (_index.Count >= capacity && _recency.Last is { } oldest)
        {
            Evict(oldest);
        }

        var entry = new Entry(key, value)
        {
            Sliding = options.SlidingExpiration,
            ExpiresAt = timeProvider.GetUtcNow() + (options.TimeToLive ?? options.SlidingExpiration ?? defaultTimeToLive),
        };
        _index[key] = _recency.AddFirst(entry);
    }

    private void Evict(LinkedListNode<Entry> node)
    {
        _index.Remove(node.Value.Key);
        _recency.Remove(node);
    }

    private sealed class Entry(string key, TValue value)
    {
        public string Key { get; } = key;

        public TValue Value { get; } = value;

        public TimeSpan? Sliding { get; init; }

        public DateTimeOffset ExpiresAt { get; set; }
    }
}
