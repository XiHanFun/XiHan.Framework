// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Caching.Distributed.Abstracts;

namespace XiHan.Framework.Caching.Distributed;

/// <summary>
/// <see cref="IDistributedLock"/> 便捷扩展。
/// </summary>
public static class DistributedLockExtensions
{
    /// <summary>
    /// 拿到锁就执行 <paramref name="action"/> 并自动释放；拿不到则跳过并返回 <see langword="false"/>。
    /// </summary>
    /// <param name="distributedLock">分布式锁</param>
    /// <param name="resourceKey">资源键</param>
    /// <param name="expiry">锁过期时间</param>
    /// <param name="action">临界区操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否抢到锁并执行</returns>
    public static async Task<bool> WithLockAsync(
        this IDistributedLock distributedLock,
        string resourceKey,
        TimeSpan expiry,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(distributedLock);
        ArgumentNullException.ThrowIfNull(action);

        await using var handle = await distributedLock.TryAcquireAsync(resourceKey, expiry, cancellationToken);
        if (handle is null)
        {
            return false;
        }

        await action(cancellationToken);
        return true;
    }

    /// <summary>
    /// 带返回值版本：拿到锁执行 <paramref name="func"/> 并返回 <c>(true, 结果)</c>；拿不到返回 <c>(false, default)</c>。
    /// </summary>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="distributedLock">分布式锁</param>
    /// <param name="resourceKey">资源键</param>
    /// <param name="expiry">锁过期时间</param>
    /// <param name="func">临界区操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否抢到锁、以及执行结果</returns>
    public static async Task<(bool Acquired, TResult? Result)> WithLockAsync<TResult>(
        this IDistributedLock distributedLock,
        string resourceKey,
        TimeSpan expiry,
        Func<CancellationToken, Task<TResult>> func,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(distributedLock);
        ArgumentNullException.ThrowIfNull(func);

        await using var handle = await distributedLock.TryAcquireAsync(resourceKey, expiry, cancellationToken);
        if (handle is null)
        {
            return (false, default);
        }

        return (true, await func(cancellationToken));
    }

    /// <summary>
    /// 等待重试直到拿到锁，或超过 <paramref name="wait"/> 仍未拿到则返回 <see langword="null"/>。
    /// </summary>
    /// <param name="distributedLock">分布式锁</param>
    /// <param name="resourceKey">资源键</param>
    /// <param name="expiry">锁过期时间</param>
    /// <param name="wait">最长等待时间</param>
    /// <param name="pollInterval">重试间隔（默认 100ms）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>锁句柄；等待超时仍未拿到返回 <see langword="null"/></returns>
    public static async Task<IDistributedLockHandle?> AcquireAsync(
        this IDistributedLock distributedLock,
        string resourceKey,
        TimeSpan expiry,
        TimeSpan wait,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(distributedLock);

        var poll = pollInterval is { } p && p > TimeSpan.Zero ? p : TimeSpan.FromMilliseconds(100);
        var deadline = DateTime.UtcNow.Add(wait);

        while (true)
        {
            var handle = await distributedLock.TryAcquireAsync(resourceKey, expiry, cancellationToken);
            if (handle is not null)
            {
                return handle;
            }

            if (DateTime.UtcNow >= deadline)
            {
                return null;
            }

            await Task.Delay(poll, cancellationToken);
        }
    }
}
