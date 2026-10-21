// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers.Binary;
using StackExchange.Redis;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Redis;

/// <summary>
/// 值的线上编码：1 字节头（0 绝对、1 滑动），滑动时跟 8 字节窗口毫秒数（小端），其后是载荷。
/// </summary>
internal static class RedisValueCodec
{
    private const byte Absolute = 0;
    private const byte Sliding = 1;

    public static byte[] Encode(ReadOnlyMemory<byte> payload, TimeSpan? sliding)
    {
        if (sliding is null)
        {
            var absolute = new byte[1 + payload.Length];
            absolute[0] = Absolute;
            payload.Span.CopyTo(absolute.AsSpan(1));
            return absolute;
        }

        var encoded = new byte[1 + sizeof(long) + payload.Length];
        encoded[0] = Sliding;
        BinaryPrimitives.WriteInt64LittleEndian(encoded.AsSpan(1), (long)sliding.Value.TotalMilliseconds);
        payload.Span.CopyTo(encoded.AsSpan(1 + sizeof(long)));
        return encoded;
    }

    public static (ReadOnlyMemory<byte> Payload, TimeSpan? Sliding) Decode(byte[] encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        if (encoded.Length == 0)
        {
            throw new InvalidOperationException("Redis 缓存值为空，不是本 Provider 写入的格式。");
        }

        return encoded[0] switch
        {
            Absolute => (new ReadOnlyMemory<byte>(encoded, 1, encoded.Length - 1), null),
            Sliding when encoded.Length >= 1 + sizeof(long) => (
                new ReadOnlyMemory<byte>(encoded, 1 + sizeof(long), encoded.Length - 1 - sizeof(long)),
                TimeSpan.FromMilliseconds(BinaryPrimitives.ReadInt64LittleEndian(encoded.AsSpan(1)))),
            _ => throw new InvalidOperationException("Redis 缓存值头部不合法，不是本 Provider 写入的格式。"),
        };
    }
}

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
