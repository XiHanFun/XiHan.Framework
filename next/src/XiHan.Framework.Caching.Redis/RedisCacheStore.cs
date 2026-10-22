// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using StackExchange.Redis;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Redis;

/// <summary>
/// 基于 StackExchange.Redis 的字节存储。连接与超时失败抛出携带 <see cref="CachingErrors.StoreUnavailable"/> 的 <see cref="FrameworkException"/>。
/// </summary>
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
    /// 条目的初始 TTL：依次取绝对存活、滑动窗口、默认存活中第一个有值的。
    /// </summary>
    private TimeSpan Expiry(CacheEntryOptions options) => options.TimeToLive ?? options.SlidingExpiration ?? _options.DefaultTimeToLive;

    /// <summary>
    /// 把连接或超时异常包装成携带 <see cref="CachingErrors.StoreUnavailable"/> 的框架异常。
    /// </summary>
    private static FrameworkException Unavailable(Exception exception)
        => new(CachingErrors.StoreUnavailable, $"The Redis operation failed: {exception.Message}", exception);
}
