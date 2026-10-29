// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

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
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class RedisCacheStore(IConnectionMultiplexer connection, RedisCachingOptions options) : ICacheStore
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
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryAddAsync(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validated();
        try
        {
            return await _connection.GetDatabase().StringSetAsync(Key(key), RedisValueCodec.Encode(value, options.SlidingExpiration), expiry: Expiry(options), when: When.NotExists).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken)
    {
        try
        {
            return await _connection.GetDatabase().KeyDeleteAsync(Key(key)).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<bool>> RemoveManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count == 0)
        {
            return [];
        }

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
            return await Task.WhenAll(deletes).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryRemoveAsync(CacheKey key, ReadOnlyMemory<byte> expectedValue, CancellationToken cancellationToken)
    {
        try
        {
            // GET 与 DEL 分两次往返会有竞争窗口，所以交给 Lua 脚本在服务端原子完成
            var result = await _connection.GetDatabase().ScriptEvaluateAsync(CompareAndDeleteScript, [Key(key)], [expectedValue.ToArray()]).ConfigureAwait(false);
            return (long)result == 1;
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw Unavailable(exception);
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
