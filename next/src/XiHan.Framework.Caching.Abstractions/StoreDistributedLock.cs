// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions;

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
