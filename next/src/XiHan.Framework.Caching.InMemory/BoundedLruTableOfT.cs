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
    /// <summary>
    /// 键到链表节点的索引；键区分大小写。
    /// </summary>
    private readonly Dictionary<string, LinkedListNode<Entry>> _index = new(StringComparer.Ordinal);

    /// <summary>
    /// 按最近访问排序的链表：头部最新，尾部最久未访问，满时从尾部淘汰。
    /// </summary>
    private readonly LinkedList<Entry> _recency = new();

    /// <summary>
    /// 保护索引与链表的锁；所有公开操作都在它之内完成。
    /// </summary>
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
    /// 读取未过期的值；不存在或已过期返回 <see langword="null"/>。命中会刷新最近使用顺序，滑动过期的条目同时延长到期时刻。
    /// </summary>
    public TValue? Get(string key)
    {
        lock (_gate)
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
    }

    /// <summary>
    /// 写入或覆盖；满时先淘汰最久未访问的条目。
    /// </summary>
    public void Set(string key, TValue value, CacheEntryOptions options)
    {
        lock (_gate)
        {
            Write(key, value, options);
        }
    }

    /// <summary>
    /// 不存在（或已过期）才写入；写入返回 <see langword="true"/>。检查与写入在同一把锁内，因此是原子的。
    /// </summary>
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
    /// 删除未过期的条目；给定 <paramref name="condition"/> 时只在当前值满足条件才删除（比较删）。删除返回 <see langword="true"/>。
    /// </summary>
    public bool Remove(string key, Func<TValue, bool>? condition = null)
    {
        lock (_gate)
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
    /// 取出未过期的节点；不存在返回 <see langword="null"/>，已过期则顺手清理并返回 <see langword="null"/>。须在锁内调用。
    /// </summary>
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
    /// 无条件写入：移除同键旧条目，按需淘汰最久未访问的条目腾出容量，再把新条目放到链表头部。须在锁内调用。
    /// </summary>
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
    /// 同时从索引与链表移除节点。须在锁内调用。
    /// </summary>
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
        /// 滑动窗口；<see langword="null"/> 表示不滑动。
        /// </summary>
        public TimeSpan? Sliding { get; init; }

        /// <summary>
        /// 到期时刻；滑动过期的条目在每次读取后更新。
        /// </summary>
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
