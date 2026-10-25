// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Caching.Distributed;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Distributed;

/// <summary>
/// 把 .NET 的 <see cref="IDistributedCache"/> 架在 <see cref="ICacheStore"/> 上：键须满足 <see cref="CacheKey"/> 文法；绝对过期换算成 <see cref="CacheEntryOptions.TimeToLive"/>，滑动过期对应 <see cref="CacheEntryOptions.SlidingExpiration"/>，两者都给时绝对过期优先（存储不能同时表达两者）；<see cref="Refresh"/> 是一次读取（读取即续期）；同步成员在适配器边界阻塞等待异步存储，调用方应优先用异步成员。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class CacheStoreDistributedCache : IDistributedCache
{
    /// <summary>
    /// 底层字节存储。
    /// </summary>
    private readonly ICacheStore _store;

    /// <summary>
    /// 时钟；绝对过期时刻据此换算成存活时间。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 创建。
    /// </summary>
    public CacheStoreDistributedCache(ICacheStore store, TimeProvider clock)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc />
    public byte[]? Get(string key) => GetAsync(key, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        var found = await _store.GetAsync(new CacheKey(key), token).ConfigureAwait(false);
        return found.TryGetValue(out var value) ? value.ToArray() : null;
    }

    /// <inheritdoc />
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => SetAsync(key, value, options, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc />
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        return _store.SetAsync(new CacheKey(key), value, Translate(options, _clock.GetUtcNow()), token).AsTask();
    }

    /// <inheritdoc />
    public void Refresh(string key) => RefreshAsync(key, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task RefreshAsync(string key, CancellationToken token = default)
        => await _store.GetAsync(new CacheKey(key), token).ConfigureAwait(false);

    /// <inheritdoc />
    public void Remove(string key) => RemoveAsync(key, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken token = default)
        => await _store.RemoveAsync(new CacheKey(key), token).ConfigureAwait(false);

    /// <summary>
    /// 把 .NET 的过期选项换算成存储的条目选项：绝对过期（时刻或相对现在，两者都给取较早者）换算成存活时间，已过去的时刻拒绝；没有绝对过期时用滑动过期；都没有则不过期。
    /// </summary>
    public static CacheEntryOptions Translate(DistributedCacheEntryOptions options, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(options);
        var absolute = options.AbsoluteExpiration;
        if (options.AbsoluteExpirationRelativeToNow is { } relative)
        {
            var at = now + relative;
            absolute = absolute is { } given && given < at ? given : at;
        }

        if (absolute is { } expiresAt)
        {
            var timeToLive = expiresAt - now;
            return timeToLive > TimeSpan.Zero
                ? CacheEntryOptions.Expires(timeToLive)
                : throw new ArgumentOutOfRangeException(nameof(options), expiresAt, "The absolute expiration value must be in the future.");
        }

        return options.SlidingExpiration is { } sliding ? CacheEntryOptions.Sliding(sliding) : CacheEntryOptions.Default;
    }
}
