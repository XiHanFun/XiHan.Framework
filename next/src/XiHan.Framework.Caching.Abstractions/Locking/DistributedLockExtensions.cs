// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Locking;

/// <summary>
/// 分布式锁的结果式获取。
/// </summary>
/// <remarks>
/// 要把「锁被占用」当作调用失败返回给调用方时用它，得到登记好的错误码与可本地化的消息；只想知道拿没拿到用 <see cref="IDistributedLock.TryAcquireAsync"/>。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class DistributedLockExtensions
{
    /// <summary>
    /// 获取锁，被占用时返回 <see cref="CachingErrors.LockHeld"/>。
    /// </summary>
    /// <remarks>
    /// 冲突的消息键是 <see cref="CachingMessages.LockHeld"/>，参数 <c>resource</c>；其余语义同 <see cref="IDistributedLock.TryAcquireAsync"/>，存储不可达照样抛出携带 <see cref="CachingErrors.StoreUnavailable"/> 的异常。
    /// </remarks>
    public static async ValueTask<Result<LockHandle>> AcquireAsync(this IDistributedLock locks, CacheKey resource, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(locks);
        var acquired = await locks.TryAcquireAsync(resource, timeToLive, cancellationToken).ConfigureAwait(false);
        return acquired.TryGetValue(out var handle)
            ? Result.Success(handle)
            : Error.Conflict(CachingErrors.LockHeld, CachingMessages.LockHeld, MessageArguments.Of(("resource", resource.Value)));
    }
}
