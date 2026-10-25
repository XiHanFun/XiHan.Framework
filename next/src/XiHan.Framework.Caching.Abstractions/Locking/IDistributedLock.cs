// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Locking;

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
