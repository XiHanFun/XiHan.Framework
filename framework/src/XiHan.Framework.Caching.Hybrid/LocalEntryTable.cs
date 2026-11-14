// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Caching.Hybrid;

/// <summary>
/// L1：保存对象引用的有界 LRU 表
/// </summary>
/// <remarks>
/// 每个条目带绝对过期时刻；满时淘汰最久未访问的条目；过期条目在访问时清理。单把锁保护，操作都是内存级别的短临界区。
/// 另记失效轮次：键按散列分到固定数目的条带，删除一个键让它所在条带的轮次加一，按前缀删除让全部条带加一。从 L2 读回后回填前核对轮次，读的途中有过失效就不回填（ADR-0229）
/// </remarks>
internal sealed class LocalEntryTable
{
    /// <summary>
    /// 失效轮次的条带数
    /// </summary>
    /// <remarks>
    /// 条带越多，无关键的失效越少挡住回填；固定数目让内存有界
    /// </remarks>
    private const int EpochStripes = 64;

    /// <summary>
    /// 容量
    /// </summary>
    private readonly int _capacity;

    /// <summary>
    /// 时钟
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 键 → 链表节点
    /// </summary>
    /// <remarks>
    /// 节点越靠近链表头越是最近访问
    /// </remarks>
    private readonly Dictionary<string, LinkedListNode<Entry>> _entries = new(StringComparer.Ordinal);

    /// <summary>
    /// LRU 顺序
    /// </summary>
    private readonly LinkedList<Entry> _order = new();

    /// <summary>
    /// 每个条带的失效轮次
    /// </summary>
    private readonly long[] _epochs = new long[EpochStripes];

    /// <summary>
    /// 锁
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 创建
    /// </summary>
    public LocalEntryTable(int capacity, TimeProvider clock)
    {
        _capacity = capacity;
        _clock = clock;
    }

    /// <summary>
    /// 当前条目数（含尚未清理的过期条目）
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
    /// 取值
    /// </summary>
    /// <remarks>
    /// 不存在或已过期为 <see langword="null"/>。命中即移到最近访问
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
    /// 键所在条带当前的失效轮次
    /// </summary>
    /// <remarks>
    /// 读 L2 之前取，回填时交给 <see cref="SetIfUnchanged"/> 核对
    /// </remarks>
    public long EpochOf(string key)
    {
        lock (_gate)
        {
            return _epochs[Stripe(key)];
        }
    }

    /// <summary>
    /// 写入或覆盖
    /// </summary>
    /// <remarks>
    /// 容量满时先淘汰链表尾部
    /// </remarks>
    public void Set(string key, object value, DateTimeOffset expiresAt)
    {
        lock (_gate)
        {
            SetCore(key, value, expiresAt);
        }
    }

    /// <summary>
    /// 键所在条带的失效轮次还是 <paramref name="epoch"/> 时才写入
    /// </summary>
    /// <remarks>
    /// 回填用：读 L2 的途中收到过这个条带的失效（别的实例刚写了这个键），读回的值可能早于那次写入，不回填，下一次读取再去 L2。返回是否写入
    /// </remarks>
    public bool SetIfUnchanged(string key, object value, DateTimeOffset expiresAt, long epoch)
    {
        lock (_gate)
        {
            // 核对与写入在同一个临界区：失效要么在核对之前（不写），要么在写入之后（把刚写的删掉）
            if (_epochs[Stripe(key)] != epoch)
            {
                return false;
            }

            SetCore(key, value, expiresAt);
            return true;
        }
    }

    /// <summary>
    /// 删除前缀下的全部条目，返回删除数
    /// </summary>
    /// <remarks>
    /// 全部条带的失效轮次加一：前缀可能覆盖任意条带里的键
    /// </remarks>
    public int RemovePrefix(string prefix)
    {
        lock (_gate)
        {
            for (var stripe = 0; stripe < _epochs.Length; stripe++)
            {
                _epochs[stripe]++;
            }

            var matched = _entries.Where(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal)).Select(static pair => pair.Value).ToList();
            foreach (var node in matched)
            {
                _entries.Remove(node.Value.Key);
                _order.Remove(node);
            }

            return matched.Count;
        }
    }

    /// <summary>
    /// 删除
    /// </summary>
    /// <remarks>
    /// 返回是否存在过（过期但未清理的也算存在）。不论是否存在，键所在条带的失效轮次都加一：正在读 L2 的回填不能把旧值放回来
    /// </remarks>
    public bool Remove(string key)
    {
        lock (_gate)
        {
            _epochs[Stripe(key)]++;
            if (!_entries.Remove(key, out var node))
            {
                return false;
            }

            _order.Remove(node);
            return true;
        }
    }

    /// <summary>
    /// 写入或覆盖，调用方已持锁
    /// </summary>
    private void SetCore(string key, object value, DateTimeOffset expiresAt)
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

    /// <summary>
    /// 键所在的条带
    /// </summary>
    private static int Stripe(string key) => (int)((uint)StringComparer.Ordinal.GetHashCode(key) % EpochStripes);

    /// <summary>
    /// 一个条目
    /// </summary>
    private sealed record Entry(string Key, object Value, DateTimeOffset ExpiresAt);
}
