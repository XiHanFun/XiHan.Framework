// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Locking;

/// <summary>
/// 已获取的锁
/// </summary>
/// <remarks>
/// 释放一次；释放或到期后再释放返回 <see langword="false"/>。抽象基类：锁的实现派生它并实现 <see cref="ReleaseCoreAsync"/>，包外也能实现 <see cref="IDistributedLock"/>；以后加续约只加虚成员，不破坏已有实现（ADR-0165）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public abstract class LockHandle : IAsyncDisposable
{
    /// <summary>
    /// 是否已经调用过释放
    /// </summary>
    /// <remarks>
    /// 0 未释放、1 已释放；原子交换保证并发释放也只执行一次
    /// </remarks>
    private int _released;

    /// <summary>
    /// 由锁实现创建句柄
    /// </summary>
    /// <remarks>
    /// 调用方只通过 <see cref="IDistributedLock.TryAcquireAsync"/> 获得
    /// </remarks>
    /// <param name="resource">被锁的资源</param>
    /// <param name="token">本次持有的唯一令牌，不能为空白</param>
    protected LockHandle(CacheKey resource, string token)
    {
        if (resource == default)
        {
            throw new ArgumentException("The locked resource must not be the default value.", nameof(resource));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        Resource = resource;
        Token = token;
    }

    /// <summary>
    /// 被锁的资源
    /// </summary>
    public CacheKey Resource { get; }

    /// <summary>
    /// 本次持有的唯一令牌
    /// </summary>
    /// <remarks>
    /// 只有令牌匹配才能释放
    /// </remarks>
    public string Token { get; }

    /// <summary>
    /// 释放
    /// </summary>
    /// <remarks>
    /// 仍持有并成功删除返回 <see langword="true"/>；只执行一次，之后返回 <see langword="false"/>
    /// </remarks>
    public async ValueTask<bool> ReleaseAsync(CancellationToken cancellationToken)
    {
        // 先置位再释放：即使释放失败（锁已到期、令牌不再匹配）也不重试，句柄从此视为无效
        if (Interlocked.Exchange(ref _released, 1) == 1)
        {
            return false;
        }

        return await ReleaseCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 续约
    /// </summary>
    /// <remarks>
    /// 仍持有时把到期时刻重设为现在加 <paramref name="timeToLive"/> 并返回 <see langword="true"/>；已到期、被他人持有或已调用过释放返回 <see langword="false"/>。原子，锁在续约期间一直被持有（ADR-0215）
    /// </remarks>
    public ValueTask<bool> TryExtendAsync(TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeToLive, TimeSpan.Zero);
        // 释放过的句柄不再代表持有：即使存储里的条目恰好还在（释放失败），也不能续回来
        return Volatile.Read(ref _released) == 1 ? ValueTask.FromResult(false) : ExtendCoreAsync(timeToLive, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await ReleaseAsync(CancellationToken.None).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 按令牌比较后删除锁
    /// </summary>
    /// <remarks>
    /// 由 <see cref="ReleaseAsync"/> 至多调用一次；仍持有并删除返回 <see langword="true"/>，已到期或令牌不再匹配返回 <see langword="false"/>
    /// </remarks>
    protected abstract ValueTask<bool> ReleaseCoreAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 按令牌比较后重设到期时刻
    /// </summary>
    /// <remarks>
    /// 由 <see cref="TryExtendAsync"/> 在未释放时调用；仍持有并重设返回 <see langword="true"/>，已到期或令牌不再匹配返回 <see langword="false"/>
    /// </remarks>
    protected abstract ValueTask<bool> ExtendCoreAsync(TimeSpan timeToLive, CancellationToken cancellationToken);
}
