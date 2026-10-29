// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Caching.Hybrid;

/// <summary>
/// L1：保存对象引用的有界 LRU 表。
/// </summary>
/// <remarks>
/// 每个条目带绝对过期时刻；满时淘汰最久未访问的条目；过期条目在访问时清理。单把锁保护，操作都是内存级别的短临界区。
/// </remarks>
internal sealed class LocalEntryTable
{
    /// <summary>
    /// 容量。
    /// </summary>
    private readonly int _capacity;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 键 → 链表节点。
    /// </summary>
    /// <remarks>
    /// 节点越靠近链表头越是最近访问。
    /// </remarks>
    private readonly Dictionary<string, LinkedListNode<Entry>> _entries = new(StringComparer.Ordinal);

    /// <summary>
    /// LRU 顺序。
    /// </summary>
    private readonly LinkedList<Entry> _order = new();

    /// <summary>
    /// 锁。
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 创建。
    /// </summary>
    public LocalEntryTable(int capacity, TimeProvider clock)
    {
        _capacity = capacity;
        _clock = clock;
    }

    /// <summary>
    /// 当前条目数（含尚未清理的过期条目）。
    /// </summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// 取值。
    /// </summary>
    /// <remarks>
    /// 不存在或已过期为 <see langword="null"/>。命中即移到最近访问。
    /// </remarks>
    public object? Get(string key)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var node))
            {
                return null;
            }

            // 过期的条目当场清掉，不等淘汰
            if (node.Value.ExpiresAt <= _clock.GetUtcNow())
            {
                _order.Remove(node);
                _entries.Remove(key);
                return null;
            }

            _order.Remove(node);
            _order.AddFirst(node);
            return node.Value.Value;
        }
    }

    /// <summary>
    /// 写入或覆盖。
    /// </summary>
    /// <remarks>
    /// 容量满时先淘汰链表尾部。
    /// </remarks>
    public void Set(string key, object value, DateTimeOffset expiresAt)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                _order.Remove(existing);
                _entries.Remove(key);
            }

            // 先淘汰再插入：插入后表里最多 _capacity 条
            while (_entries.Count >= _capacity && _order.Last is { } last)
            {
                _order.RemoveLast();
                _entries.Remove(last.Value.Key);
            }

            var node = new LinkedListNode<Entry>(new Entry(key, value, expiresAt));
            _order.AddFirst(node);
            _entries[key] = node;
        }
    }

    /// <summary>
    /// 删除。
    /// </summary>
    /// <remarks>
    /// 返回是否存在过（过期但未清理的也算存在）。
    /// </remarks>
    public bool Remove(string key)
    {
        lock (_gate)
        {
            if (!_entries.Remove(key, out var node))
            {
                return false;
            }

            _order.Remove(node);
            return true;
        }
    }

    /// <summary>
    /// 一个条目。
    /// </summary>
    private sealed record Entry(string Key, object Value, DateTimeOffset ExpiresAt);
}
