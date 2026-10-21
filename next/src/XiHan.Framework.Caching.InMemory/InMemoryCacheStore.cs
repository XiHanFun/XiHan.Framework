// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.InMemory;

/// <summary>
/// 内存缓存选项。从配置节 <c>XiHan:Caching:InMemory</c> 显式读取。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record InMemoryCachingOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Caching:InMemory";

    /// <summary>
    /// 每张表（字节存储、对象缓存各一张）的条目上限；默认 10000。
    /// </summary>
    public int MaxEntries
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "条目上限必须为正。");
    } = 10_000;

    /// <summary>
    /// 未指定过期时的存活时间；默认 5 分钟。
    /// </summary>
    public TimeSpan DefaultTimeToLive
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "默认存活时间必须为正。");
    } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 从配置读取：<c>MaxEntries</c>、<c>DefaultTimeToLiveSeconds</c>。
    /// </summary>
    public static InMemoryCachingOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new InMemoryCachingOptions();
        var maxEntries = configuration[$"{ConfigurationSection}:MaxEntries"];
        if (maxEntries is not null)
        {
            options = int.TryParse(maxEntries, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 1
                ? options with { MaxEntries = value }
                : throw new ArgumentException($"{ConfigurationSection}:MaxEntries 必须是正整数，实际 '{maxEntries}'。");
        }

        var ttl = configuration[$"{ConfigurationSection}:DefaultTimeToLiveSeconds"];
        if (ttl is not null)
        {
            options = double.TryParse(ttl, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? options with { DefaultTimeToLive = TimeSpan.FromSeconds(seconds) }
                : throw new ArgumentException($"{ConfigurationSection}:DefaultTimeToLiveSeconds 必须是正数，实际 '{ttl}'。");
        }

        return options;
    }
}

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

/// <summary>
/// 进程内的类型化缓存：直接保存对象引用，不序列化，与 <c>IMemoryCache</c> 同样的约定——调用方不得修改取回的对象。
/// <see cref="Store"/> 指向同一进程的字节存储，供锁与幂等使用。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryObjectCache : ISharedCache
{
    private readonly BoundedLruTable<object> _table;

    /// <summary>
    /// 创建缓存。
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
    /// 当前对象条目数。
    /// </summary>
    public int Count => _table.Count;

    /// <inheritdoc />
    public ValueTask<Maybe<T>> GetAsync<T>(CacheKey key, CancellationToken cancellationToken)
        => ValueTask.FromResult(_table.Get(key.Value) is T value ? Maybe.Some(value) : default);

    /// <inheritdoc />
    public ValueTask SetAsync<T>(CacheKey key, T value, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        _table.Set(key.Value, value, options.Validated());
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken) => ValueTask.FromResult(_table.Remove(key.Value));

    /// <inheritdoc />
    public async ValueTask<T> GetOrCreateAsync<T>(CacheKey key, Func<CancellationToken, ValueTask<T>> factory, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(options);
        if (_table.Get(key.Value) is T hit)
        {
            return hit;
        }

        var created = await factory(cancellationToken).ConfigureAwait(false);
        _table.Set(key.Value, created, options.Validated());
        return created;
    }
}
