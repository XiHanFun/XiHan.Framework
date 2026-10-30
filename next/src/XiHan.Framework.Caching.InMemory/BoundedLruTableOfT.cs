// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions.Stores;

namespace XiHan.Framework.Caching.InMemory;

/// <summary>
/// 有界 LRU 表。
/// </summary>
/// <remarks>
/// 所有操作在一把锁内完成，过期在访问时清理，满时淘汰最久未访问的条目；批量方法一次加锁逐项处理。
/// </remarks>
internal sealed class BoundedLruTable<TValue>(int capacity, TimeSpan defaultTimeToLive, TimeProvider timeProvider)
    where TValue : class
{
    /// <summary>
    /// 键到链表节点的索引。
    /// </summary>
    /// <remarks>
    /// 键区分大小写。
    /// </remarks>
    private readonly Dictionary<string, LinkedListNode<Entry>> _index = new(StringComparer.Ordinal);

    /// <summary>
    /// 按最近访问排序的链表：头部最新，尾部最久未访问，满时从尾部淘汰。
    /// </summary>
    private readonly LinkedList<Entry> _recency = new();

    /// <summary>
    /// 保护索引与链表的锁。
    /// </summary>
    /// <remarks>
    /// 所有公开操作都在它之内完成。
    /// </remarks>
    private readonly Lock _gate = new();

    /// <summary>
    /// 当前条目数（含尚未被访问清理的过期条目）。
    /// </summary>
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

    /// <summary>
    /// 读取未过期的值。
    /// </summary>
    /// <remarks>
    /// 不存在或已过期返回 <see langword="null"/>。命中会刷新最近使用顺序，滑动过期的条目同时延长到期时刻。
    /// </remarks>
    public TValue? Get(string key)
    {
        lock (_gate)
        {
            return Read(key);
        }
    }

    /// <summary>
    /// 批量读取。
    /// </summary>
    /// <remarks>
    /// 一次加锁逐键读取，语义同 <see cref="Get"/>；结果与键顺序一一对应。
    /// </remarks>
    public TValue?[] GetMany(IReadOnlyList<string> keys)
    {
        var values = new TValue?[keys.Count];
        lock (_gate)
        {
            for (var index = 0; index < keys.Count; index++)
            {
                values[index] = Read(keys[index]);
            }
        }

        return values;
    }

    /// <summary>
    /// 写入或覆盖。
    /// </summary>
    /// <remarks>
    /// 满时先淘汰最久未访问的条目。
    /// </remarks>
    public void Set(string key, TValue value, CacheEntryOptions options)
    {
        lock (_gate)
        {
            Write(key, value, options);
        }
    }

    /// <summary>
    /// 批量写入：一次加锁逐项写入，同一键出现多次后者覆盖。
    /// </summary>
    public void SetMany(IReadOnlyList<(string Key, TValue Value, CacheEntryOptions Options)> entries)
    {
        lock (_gate)
        {
            foreach (var (key, value, options) in entries)
            {
                Write(key, value, options);
            }
        }
    }

    /// <summary>
    /// 不存在（或已过期）才写入。
    /// </summary>
    /// <remarks>
    /// 写入返回 <see langword="true"/>。检查与写入在同一把锁内，因此是原子的。
    /// </remarks>
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

    /// <summary>
    /// 删除未过期的条目。
    /// </summary>
    /// <remarks>
    /// 给定 <paramref name="condition"/> 时只在当前值满足条件才删除（比较删）。删除返回 <see langword="true"/>。
    /// </remarks>
    public bool Remove(string key, Func<TValue, bool>? condition = null)
    {
        lock (_gate)
        {
            return Delete(key, condition);
        }
    }

    /// <summary>
    /// 批量删除。
    /// </summary>
    /// <remarks>
    /// 一次加锁逐键删除；结果与键顺序一一对应，同一键出现多次第二次为 <see langword="false"/>。
    /// </remarks>
    public bool[] RemoveMany(IReadOnlyList<string> keys)
    {
        var results = new bool[keys.Count];
        lock (_gate)
        {
            for (var index = 0; index < keys.Count; index++)
            {
                results[index] = Delete(keys[index], null);
            }
        }

        return results;
    }

    /// <summary>
    /// 前缀下仍存活的键。
    /// </summary>
    /// <remarks>
    /// 按序数排序，从 <paramref name="after"/> 之后取至多 <paramref name="limit"/> 个；不更新访问顺序。
    /// </remarks>
    public IReadOnlyList<string> KeysWithPrefix(string prefix, string? after, int limit)
    {
        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            return [.. _index.Values
                .Where(node => node.Value.Key.StartsWith(prefix, StringComparison.Ordinal) && node.Value.ExpiresAt > now && (after is null || string.CompareOrdinal(node.Value.Key, after) > 0))
                .Select(static node => node.Value.Key)
                .Order(StringComparer.Ordinal)
                .Take(limit)];
        }
    }

    /// <summary>
    /// 读一个仍存活的条目，不更新访问顺序与滑动过期。
    /// </summary>
    /// <remarks>
    /// 不存在或已过期为 <see langword="null"/>。
    /// </remarks>
    public (TValue Value, DateTimeOffset ExpiresAt, TimeSpan? Sliding)? Peek(string key)
    {
        lock (_gate)
        {
            return _index.TryGetValue(key, out var node) && node.Value.ExpiresAt > timeProvider.GetUtcNow()
                ? (node.Value.Value, node.Value.ExpiresAt, node.Value.Sliding)
                : null;
        }
    }

    /// <summary>
    /// 删除前缀下的全部条目，返回其中仍存活的个数。
    /// </summary>
    public int RemovePrefix(string prefix)
    {
        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            var matched = _index.Values.Where(node => node.Value.Key.StartsWith(prefix, StringComparison.Ordinal)).ToList();
            var live = 0;
            foreach (var node in matched)
            {
                live += node.Value.ExpiresAt > now ? 1 : 0;
                Evict(node);
            }

            return live;
        }
    }

    /// <summary>
    /// 清空全部条目。
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            _index.Clear();
            _recency.Clear();
        }
    }

    /// <summary>
    /// 读取未过期的值，命中时刷新最近使用顺序并延长滑动过期。
    /// </summary>
    /// <remarks>
    /// 须在锁内调用。
    /// </remarks>
    private TValue? Read(string key)
    {
        var node = Live(key);
        if (node is null)
        {
            return null;
        }

        // 滑动过期：每次命中都把到期时刻推后一个窗口
        if (node.Value.Sliding is { } sliding)
        {
            node.Value.ExpiresAt = timeProvider.GetUtcNow() + sliding;
        }

        // 移到链表头部表示最近使用
        _recency.Remove(node);
        _recency.AddFirst(node);
        return node.Value.Value;
    }

    /// <summary>
    /// 删除未过期的条目，给定条件时只在当前值满足条件才删除。
    /// </summary>
    /// <remarks>
    /// 须在锁内调用。
    /// </remarks>
    private bool Delete(string key, Func<TValue, bool>? condition)
    {
        // 已过期的条目视为不存在，比较删也不会匹配到它
        var node = Live(key);
        if (node is null || (condition is not null && !condition(node.Value.Value)))
        {
            return false;
        }

        Evict(node);
        return true;
    }

    /// <summary>
    /// 取出未过期的节点。
    /// </summary>
    /// <remarks>
    /// 不存在返回 <see langword="null"/>，已过期则顺手清理并返回 <see langword="null"/>。须在锁内调用。
    /// </remarks>
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

        // 惰性过期：没有后台清理线程，访问时发现到期就立即移除
        Evict(node);
        return null;
    }

    /// <summary>
    /// 无条件写入。
    /// </summary>
    /// <remarks>
    /// 移除同键旧条目，按需淘汰最久未访问的条目腾出容量，再把新条目放到链表头部。须在锁内调用。
    /// </remarks>
    private void Write(string key, TValue value, CacheEntryOptions options)
    {
        // 覆盖写先移除旧节点，避免同键占两个位置
        if (_index.TryGetValue(key, out var existing))
        {
            Evict(existing);
        }

        // 容量有界：从链表尾部淘汰最久未访问的条目，直到腾出一个位置
        while (_index.Count >= capacity && _recency.Last is { } oldest)
        {
            Evict(oldest);
        }

        // 到期时刻的取值优先级：绝对存活 > 滑动窗口 > 表的默认存活；滑动窗口另存一份供读取时延长
        var entry = new Entry(key, value)
        {
            Sliding = options.SlidingExpiration,
            ExpiresAt = timeProvider.GetUtcNow() + (options.TimeToLive ?? options.SlidingExpiration ?? defaultTimeToLive),
        };
        _index[key] = _recency.AddFirst(entry);
    }

    /// <summary>
    /// 同时从索引与链表移除节点。
    /// </summary>
    /// <remarks>
    /// 须在锁内调用。
    /// </remarks>
    private void Evict(LinkedListNode<Entry> node)
    {
        _index.Remove(node.Value.Key);
        _recency.Remove(node);
    }

    /// <summary>
    /// 表中的一个条目：键、值与过期信息。
    /// </summary>
    private sealed class Entry(string key, TValue value)
    {
        /// <summary>
        /// 条目的键。
        /// </summary>
        public string Key { get; } = key;

        /// <summary>
        /// 条目的值。
        /// </summary>
        public TValue Value { get; } = value;

        /// <summary>
        /// 滑动窗口。
        /// </summary>
        /// <remarks>
        /// <see langword="null"/> 表示不滑动。
        /// </remarks>
        public TimeSpan? Sliding { get; init; }

        /// <summary>
        /// 到期时刻。
        /// </summary>
        /// <remarks>
        /// 滑动过期的条目在每次读取后更新。
        /// </remarks>
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
