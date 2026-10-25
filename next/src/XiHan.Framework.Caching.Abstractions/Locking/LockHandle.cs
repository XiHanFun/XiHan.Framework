// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Locking;

/// <summary>
/// 已获取的锁。释放一次；释放或到期后再释放返回 <see langword="false"/>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class LockHandle : IAsyncDisposable
{
    /// <summary>
    /// 释放回调：按令牌比较删除锁条目，仍持有时返回 <see langword="true"/>。
    /// </summary>
    private readonly Func<CancellationToken, ValueTask<bool>> _release;

    /// <summary>
    /// 是否已经调用过释放；保证释放只执行一次。
    /// </summary>
    private bool _released;

    /// <summary>
    /// 由锁实现创建句柄；调用方只通过 <see cref="IDistributedLock.TryAcquireAsync"/> 获得。
    /// </summary>
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

        // 先置位再释放：即使释放失败（锁已到期、令牌不再匹配）也不重试，句柄从此视为无效
        _released = true;
        return await _release(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await ReleaseAsync(CancellationToken.None).ConfigureAwait(false);
}
