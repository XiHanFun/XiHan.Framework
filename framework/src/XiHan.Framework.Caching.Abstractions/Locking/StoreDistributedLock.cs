// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Locking;

/// <summary>
/// 建立在 <see cref="ICacheStore"/> 的"不存在才写 + 比较删"之上的锁，任何 Provider 都能复用
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class StoreDistributedLock(ICacheStore store) : IDistributedLock
{
    /// <summary>
    /// 锁条目的键前缀，与普通缓存条目隔开
    /// </summary>
    private const string Prefix = "lock:";

    /// <summary>
    /// 承载锁条目的字节存储
    /// </summary>
    private readonly ICacheStore _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <inheritdoc />
    public async ValueTask<Maybe<LockHandle>> TryAcquireAsync(CacheKey resource, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeToLive, TimeSpan.Zero);
        // 每次获取生成唯一令牌并作为条目的值：释放时按值比较删，锁到期后被他人获取会换成新令牌，旧句柄就无法误删
        var key = new CacheKey(Prefix + resource.Value);
        var token = Guid.NewGuid().ToString("N");
        var tokenBytes = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(token));
        var acquired = await _store.TryAddAsync(key, tokenBytes, CacheEntryOptions.Expires(timeToLive), cancellationToken).ConfigureAwait(false);
        return acquired
            ? Maybe.Some<LockHandle>(new StoreLockHandle(_store, key, resource, token, tokenBytes))
            : default;
    }

    /// <summary>
    /// 存在缓存存储里的锁的句柄
    /// </summary>
    /// <remarks>
    /// 释放按令牌比较删除锁条目：锁到期后被他人获取会换成新令牌，旧句柄删不掉别人的锁
    /// </remarks>
    private sealed class StoreLockHandle(ICacheStore store, CacheKey entry, CacheKey resource, string token, ReadOnlyMemory<byte> tokenBytes) : LockHandle(resource, token)
    {
        /// <inheritdoc />
        protected override ValueTask<bool> ReleaseCoreAsync(CancellationToken cancellationToken) => store.TryRemoveAsync(entry, tokenBytes, cancellationToken);
    }
}
