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
    // 比较载荷（跳过头部）后删除，原子
    private const string CompareAndDeleteScript = """
        local v = redis.call('GET', KEYS[1])
        if not v then return 0 end
        local off = 2
        if string.byte(v, 1) == 1 then off = 10 end
        if string.sub(v, off) == ARGV[1] then return redis.call('DEL', KEYS[1]) end
        return 0
        """;

    private readonly IConnectionMultiplexer _connection = connection ?? throw new ArgumentNullException(nameof(connection));
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
            var result = await _connection.GetDatabase().ScriptEvaluateAsync(CompareAndDeleteScript, [Key(key)], [expectedValue.ToArray()]).ConfigureAwait(false);
            return (long)result == 1;
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw Unavailable(exception);
        }
    }

    private RedisKey Key(CacheKey key) => _options.KeyPrefix + key.Value;

    private TimeSpan Expiry(CacheEntryOptions options) => options.TimeToLive ?? options.SlidingExpiration ?? _options.DefaultTimeToLive;

    private static FrameworkException Unavailable(Exception exception)
        => new(CachingErrors.StoreUnavailable, $"Redis 操作失败：{exception.Message}", exception);
}
