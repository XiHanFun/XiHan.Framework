// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using StackExchange.Redis;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Redis;

/// <summary>
/// 基于 StackExchange.Redis 的字节存储。
/// </summary>
/// <remarks>
/// 连接与超时失败抛出携带 <see cref="CachingErrors.StoreUnavailable"/> 的 <see cref="FrameworkException"/>。批量读取用 <c>MGET</c>，批量写入与删除把全部命令放进一个批次、一次往返。
/// 键管理按单个端点的 <c>SCAN</c> 分页；写入与删除后通知本实例的订阅者，并以发后不等的方式向频道 <c>&lt;KeyPrefix&gt;__changes</c> 发布给其他实例（ADR-0055）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class RedisCacheStore(IConnectionMultiplexer connection, RedisCachingOptions options) : ICacheStore, ICacheStoreInspector, ICacheChangeFeed, IDisposable
{
    /// <summary>
    /// 服务端比较删脚本：跳过编码头部后比较载荷，相同才删除，整段在 Redis 内原子执行。
    /// </summary>
    private const string CompareAndDeleteScript = """
        local v = redis.call('GET', KEYS[1])
        if not v then return 0 end
        local off = 2
        if string.byte(v, 1) == 1 then off = 10 end
        if string.sub(v, off) == ARGV[1] then return redis.call('DEL', KEYS[1]) end
        return 0
        """;

    /// <summary>
    /// 由能力包管理生命周期的 Redis 连接。
    /// </summary>
    private readonly IConnectionMultiplexer _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    /// <summary>
    /// 键前缀与默认存活时间。
    /// </summary>
    private readonly RedisCachingOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// 本实例的标识。
    /// </summary>
    /// <remarks>
    /// 写进发布的通知，收到自己发的通知时跳过（本实例的订阅者已在写入时同步收到）。
    /// </remarks>
    private readonly string _origin = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 保护订阅表。
    /// </summary>
    private readonly Lock _subscriptions = new();

    /// <summary>
    /// 订阅者。
    /// </summary>
    /// <remarks>
    /// 写时复制，通知时不加锁读。
    /// </remarks>
    private Action<CacheChange>[] _handlers = [];

    /// <summary>
    /// 是否已订阅变更频道。
    /// </summary>
    private bool _listening;

    /// <inheritdoc />
    public async ValueTask<Maybe<ReadOnlyMemory<byte>>> GetAsync(CacheKey key, CancellationToken cancellationToken)
    {
        try
        {
            var redisKey = Key(key);
            var raw = (byte[]?)await _connection.GetDatabase().StringGetAsync(redisKey).ConfigureAwait(false);
            if (raw is null)
            {
                return default;
            }

            // 滑动窗口随值一起存在头部里，读取时据此重设 TTL，不需要额外查询条目的选项
            var (payload, sliding) = RedisValueCodec.Decode(raw);
            if (sliding is { } window)
            {
                await _connection.GetDatabase().KeyExpireAsync(redisKey, window).ConfigureAwait(false);
            }

            return Maybe.Some(payload);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Maybe<ReadOnlyMemory<byte>>>> GetManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count == 0)
        {
            return [];
        }

        try
        {
            var redisKeys = Keys(keys);
            var database = _connection.GetDatabase();
            // MGET 一次往返取回全部值；命中的滑动过期条目再放进一个批次统一重设 TTL，第二次往返只在有滑动条目时发生
            var raw = await database.StringGetAsync(redisKeys).ConfigureAwait(false);
            var results = new Maybe<ReadOnlyMemory<byte>>[keys.Count];
            IBatch? batch = null;
            List<Task>? extensions = null;
            for (var index = 0; index < raw.Length; index++)
            {
                if ((byte[]?)raw[index] is not { } bytes)
                {
                    continue;
                }

                var (payload, sliding) = RedisValueCodec.Decode(bytes);
                if (sliding is { } window)
                {
                    batch ??= database.CreateBatch();
                    (extensions ??= []).Add(batch.KeyExpireAsync(redisKeys[index], window));
                }

                results[index] = Maybe.Some(payload);
            }

            if (batch is not null)
            {
                batch.Execute();
                await Task.WhenAll(extensions!).ConfigureAwait(false);
            }

            return results;
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask SetAsync(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validated();
        try
        {
            await _connection.GetDatabase().StringSetAsync(Key(key), RedisValueCodec.Encode(value, options.SlidingExpiration), expiry: Expiry(options)).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw Unavailable(exception);
        }

        Notify(CacheChange.ForKeys([key]));
    }

    /// <inheritdoc />
    public async ValueTask SetManyAsync(IReadOnlyList<CacheEntry> entries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        // 先校验全部选项：任一项不合法整批不写
        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
            entry.Options.Validated();
        }

        if (entries.Count == 0)
        {
            return;
        }

        try
        {
            // 每项的过期各不相同，MSET 带不了过期：把全部 SET 放进一个批次一次往返，按顺序执行，同一键后者覆盖
            var batch = _connection.GetDatabase().CreateBatch();
            var writes = new Task[entries.Count];
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                writes[index] = batch.StringSetAsync(Key(entry.Key), RedisValueCodec.Encode(entry.Value, entry.Options.SlidingExpiration), expiry: Expiry(entry.Options));
            }

            batch.Execute();
            await Task.WhenAll(writes).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw Unavailable(exception);
        }

        Notify(CacheChange.ForKeys([.. entries.Select(static entry => entry.Key)]));
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryAddAsync(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validated();
        bool added;
        try
        {
            added = await _connection.GetDatabase().StringSetAsync(Key(key), RedisValueCodec.Encode(value, options.SlidingExpiration), expiry: Expiry(options), when: When.NotExists).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw Unavailable(exception);
        }

        if (added)
        {
            Notify(CacheChange.ForKeys([key]));
        }

        return added;
    }

    /// <inheritdoc />
    public async ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken)
    {
        bool removed;
        try
        {
            removed = await _connection.GetDatabase().KeyDeleteAsync(Key(key)).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw Unavailable(exception);
        }

        if (removed)
        {
            Notify(CacheChange.ForKeys([key]));
        }

        return removed;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<bool>> RemoveManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count == 0)
        {
            return [];
        }

        bool[] removed;
        try
        {
            // 多键 DEL 只返回总数；要逐键结果就把每个 DEL 放进同一批次，一次往返、按顺序执行，同一键第二次自然为假
            var batch = _connection.GetDatabase().CreateBatch();
            var deletes = new Task<bool>[keys.Count];
            for (var index = 0; index < keys.Count; index++)
            {
                deletes[index] = batch.KeyDeleteAsync(Key(keys[index]));
            }

            batch.Execute();
            removed = await Task.WhenAll(deletes).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw Unavailable(exception);
        }

        var changed = keys.Where((_, index) => removed[index]).ToList();
        if (changed.Count > 0)
        {
            Notify(CacheChange.ForKeys(changed));
        }

        return removed;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryRemoveAsync(CacheKey key, ReadOnlyMemory<byte> expectedValue, CancellationToken cancellationToken)
    {
        bool removed;
        try
        {
            // GET 与 DEL 分两次往返会有竞争窗口，所以交给 Lua 脚本在服务端原子完成
            var result = await _connection.GetDatabase().ScriptEvaluateAsync(CompareAndDeleteScript, [Key(key)], [expectedValue.ToArray()]).ConfigureAwait(false);
            removed = (long)result == 1;
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw Unavailable(exception);
        }

        if (removed)
        {
            Notify(CacheChange.ForKeys([key]));
        }

        return removed;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 按单个端点的 <c>SCAN</c> 分页：续页标记是 SCAN 游标，<see cref="CacheKeyQuery.Limit"/> 是提示值，同一个键可能出现在两页；前缀里的 glob 字符转义。
    /// </remarks>
    public async ValueTask<CacheKeyPage> ListKeysAsync(CacheKeyQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var cursor = query.Continuation ?? "0";
        if (!ulong.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            throw new ArgumentException($"The continuation '{cursor}' is not a Redis SCAN cursor.", nameof(query));
        }

        try
        {
            var (next, keys) = await ScanAsync(cursor, query.Prefix, query.Limit).ConfigureAwait(false);
            var page = keys.Select(StripPrefix).Where(static key => CacheKey.IsValid(key)).Select(static key => new CacheKey(key!)).ToList();
            return new CacheKeyPage(page, next == "0" ? null : next);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Maybe<CacheEntryInfo>> InspectAsync(CacheKey key, CancellationToken cancellationToken)
    {
        try
        {
            // 三条命令放进一个批次：剩余存活、编码后的长度、头部（判断滑动与头部长度）
            var batch = _connection.GetDatabase().CreateBatch();
            var ttl = batch.KeyTimeToLiveAsync(Key(key));
            var length = batch.StringLengthAsync(Key(key));
            var head = batch.StringGetRangeAsync(Key(key), 0, sizeof(long));
            batch.Execute();
            await Task.WhenAll(ttl, length, head).ConfigureAwait(false);
            if (await length.ConfigureAwait(false) == 0)
            {
                return Maybe.None<CacheEntryInfo>();
            }

            var (headerLength, sliding) = RedisValueCodec.Header((byte[])(await head.ConfigureAwait(false))!);
            return Maybe.Some(new CacheEntryInfo(key, await length.ConfigureAwait(false) - headerLength, await ttl.ConfigureAwait(false), sliding));
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<long> RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(prefix) || !CacheKey.IsValidPrefix(prefix))
        {
            throw new ArgumentException($"Cache key prefix '{prefix}' is invalid or empty.", nameof(prefix));
        }

        long removed = 0;
        try
        {
            // 扫完整个游标：每一页的键当场删除（SCAN 允许遍历期间删除）
            var cursor = "0";
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                (cursor, var keys) = await ScanAsync(cursor, prefix, ScanBatchSize).ConfigureAwait(false);
                if (keys.Count > 0)
                {
                    removed += await _connection.GetDatabase().KeyDeleteAsync([.. keys]).ConfigureAwait(false);
                }
            }
            while (cursor != "0");
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw Unavailable(exception);
        }

        Notify(CacheChange.ForPrefix(prefix));
        return removed;
    }

    /// <inheritdoc />
    public IDisposable Subscribe(Action<CacheChange> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_subscriptions)
        {
            _handlers = [.. _handlers, handler];
            if (!_listening)
            {
                // 其他实例的变更经频道转发；订阅在第一次有人关心时才建
                _connection.GetSubscriber().Subscribe(Channel, (_, message) => OnMessage(message));
                _listening = true;
            }
        }

        return new Subscription(this, handler);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_subscriptions)
        {
            if (!_listening)
            {
                return;
            }

            _listening = false;
            _handlers = [];
        }

        try
        {
            _connection.GetSubscriber().Unsubscribe(Channel);
        }
        catch (ObjectDisposedException)
        {
            // 连接先于存储释放：订阅已随连接结束
        }
    }

    /// <summary>
    /// SCAN 一页时的批大小提示。
    /// </summary>
    private const int ScanBatchSize = 1000;

    /// <summary>
    /// 变更频道。
    /// </summary>
    private RedisChannel Channel => RedisChannel.Literal(_options.KeyPrefix + "__changes");

    /// <summary>
    /// 按前缀扫一页，返回下一个游标与本页的 Redis 键。
    /// </summary>
    private async Task<(string Next, List<RedisKey> Keys)> ScanAsync(string cursor, string prefix, int count)
    {
        var result = await _connection.GetDatabase().ExecuteAsync("SCAN", cursor, "MATCH", Glob(_options.KeyPrefix + prefix) + "*", "COUNT", count).ConfigureAwait(false);
        var parts = (RedisResult[])result!;
        var keys = ((RedisResult[])parts[1]!).Select(static item => (RedisKey)(string)item!).ToList();
        return ((string)parts[0]!, keys);
    }

    /// <summary>
    /// 去掉配置的前缀，得到缓存键。
    /// </summary>
    /// <remarks>
    /// 不是本前缀下的为 <see langword="null"/>。
    /// </remarks>
    private string? StripPrefix(RedisKey key)
    {
        var text = (string?)key;
        return text is not null && text.StartsWith(_options.KeyPrefix, StringComparison.Ordinal) ? text[_options.KeyPrefix.Length..] : null;
    }

    /// <summary>
    /// 转义 glob 的特殊字符。
    /// </summary>
    private static string Glob(string literal)
    {
        var builder = new StringBuilder(literal.Length + 4);
        foreach (var ch in literal)
        {
            if (ch is '*' or '?' or '[' or ']' or '\\')
            {
                builder.Append('\\');
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    /// <summary>
    /// 通知本实例的订阅者，并向其他实例发布。
    /// </summary>
    private void Notify(CacheChange change)
    {
        foreach (var handler in Volatile.Read(ref _handlers))
        {
            handler(change);
        }

        if (!_options.PublishChanges)
        {
            return;
        }

        try
        {
            _connection.GetSubscriber().Publish(Channel, Encode(change), CommandFlags.FireAndForget);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            // 通知是尽力而为的（规范：可能丢失），写入本身已经成功，不因此报错
        }
    }

    /// <summary>
    /// 收到频道消息：不是本实例发的就交给订阅者。
    /// </summary>
    private void OnMessage(RedisValue message)
    {
        if (Decode(message, _origin) is { } change)
        {
            foreach (var handler in Volatile.Read(ref _handlers))
            {
                handler(change);
            }
        }
    }

    /// <summary>
    /// 编码通知：发出者、种类（<c>k</c> 键 / <c>p</c> 前缀）与各行内容。
    /// </summary>
    /// <remarks>
    /// 键与前缀不含空白，换行即是可靠的分隔符。
    /// </remarks>
    private string Encode(CacheChange change)
        => change.Prefix is { } prefix
            ? _origin + "\np\n" + prefix
            : _origin + "\nk\n" + string.Join('\n', change.Keys.Select(static key => key.Value));

    /// <summary>
    /// 解码通知。
    /// </summary>
    /// <remarks>
    /// 本实例发的或格式不对为 <see langword="null"/>。
    /// </remarks>
    private static CacheChange? Decode(RedisValue message, string origin)
    {
        var lines = ((string?)message ?? string.Empty).Split('\n');
        if (lines.Length < 3 || string.Equals(lines[0], origin, StringComparison.Ordinal))
        {
            return null;
        }

        return lines[1] switch
        {
            "p" when lines[2].Length > 0 && CacheKey.IsValidPrefix(lines[2]) => CacheChange.ForPrefix(lines[2]),
            "k" => CacheChange.ForKeys([.. lines.Skip(2).Where(static line => CacheKey.IsValid(line)).Select(static line => new CacheKey(line))]),
            _ => null,
        };
    }

    /// <summary>
    /// 一个订阅。
    /// </summary>
    /// <remarks>
    /// 释放即退订。
    /// </remarks>
    private sealed class Subscription(RedisCacheStore store, Action<CacheChange> handler) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose()
        {
            lock (store._subscriptions)
            {
                store._handlers = [.. store._handlers.Where(existing => existing != handler)];
            }
        }
    }

    /// <summary>
    /// 给缓存键加上配置的前缀，得到 Redis 键。
    /// </summary>
    private RedisKey Key(CacheKey key) => _options.KeyPrefix + key.Value;

    /// <summary>
    /// 给每个缓存键加前缀。
    /// </summary>
    private RedisKey[] Keys(IReadOnlyList<CacheKey> keys)
    {
        var redisKeys = new RedisKey[keys.Count];
        for (var index = 0; index < keys.Count; index++)
        {
            redisKeys[index] = Key(keys[index]);
        }

        return redisKeys;
    }

    /// <summary>
    /// 条目的初始 TTL：依次取绝对存活、滑动窗口、默认存活中第一个有值的。
    /// </summary>
    private TimeSpan Expiry(CacheEntryOptions options) => options.TimeToLive ?? options.SlidingExpiration ?? _options.DefaultTimeToLive;

    /// <summary>
    /// 把连接或超时异常包装成携带 <see cref="CachingErrors.StoreUnavailable"/> 的框架异常。
    /// </summary>
    private static FrameworkException Unavailable(Exception exception)
        => new(CachingErrors.StoreUnavailable, $"The Redis operation failed: {exception.Message}", exception);
}
