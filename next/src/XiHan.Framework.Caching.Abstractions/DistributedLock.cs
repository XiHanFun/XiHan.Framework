// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions;

/// <summary>
/// 租约式分布式锁：获取即持有到释放或存活时间到期；不可重入、不续约。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IDistributedLock
{
    /// <summary>
    /// 尝试获取；被占用时无值。
    /// </summary>
    ValueTask<Maybe<LockHandle>> TryAcquireAsync(CacheKey resource, TimeSpan timeToLive, CancellationToken cancellationToken);
}

/// <summary>
/// 已获取的锁。释放一次；释放或到期后再释放返回 <see langword="false"/>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class LockHandle : IAsyncDisposable
{
    private readonly Func<CancellationToken, ValueTask<bool>> _release;
    private bool _released;

    internal LockHandle(CacheKey resource, string token, Func<CancellationToken, ValueTask<bool>> release)
    {
        Resource = resource;
        Token = token;
        _release = release;
    }

    /// <summary>
    /// 被锁的资源。
    /// </summary>
    public CacheKey Resource { get; }

    /// <summary>
    /// 本次持有的唯一令牌；只有令牌匹配才能释放。
    /// </summary>
    public string Token { get; }

    /// <summary>
    /// 释放；仍持有并成功删除返回 <see langword="true"/>。
    /// </summary>
    public async ValueTask<bool> ReleaseAsync(CancellationToken cancellationToken)
    {
        if (_released)
        {
            return false;
        }

        _released = true;
        return await _release(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await ReleaseAsync(CancellationToken.None).ConfigureAwait(false);
}

/// <summary>
/// 建立在 <see cref="ICacheStore"/> 的"不存在才写 + 比较删"之上的锁，任何 Provider 都能复用。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class StoreDistributedLock(ICacheStore store) : IDistributedLock
{
    private const string Prefix = "lock:";

    private readonly ICacheStore _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <inheritdoc />
    public async ValueTask<Maybe<LockHandle>> TryAcquireAsync(CacheKey resource, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeToLive, TimeSpan.Zero);
        var key = new CacheKey(Prefix + resource.Value);
        var token = Guid.NewGuid().ToString("N");
        var tokenBytes = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(token));
        var acquired = await _store.TryAddAsync(key, tokenBytes, CacheEntryOptions.Expires(timeToLive), cancellationToken).ConfigureAwait(false);
        return acquired
            ? Maybe.Some(new LockHandle(resource, token, ct => _store.TryRemoveAsync(key, tokenBytes, ct)))
            : default;
    }
}
