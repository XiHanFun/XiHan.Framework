#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IDistributedLock
// Guid:3f4a5b6c-7d8e-49a0-b1c2-3d4e5f6a7b8c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/06/16 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Caching.Distributed.Abstracts;

/// <summary>
/// 分布式锁。
/// </summary>
/// <remarks>
/// Redis 启用时为 Redis 实现（<c>SET NX PX</c> + 释放时校验持有者删除，跨实例互斥）；
/// 未启用时回退进程内实现（仅单实例有效）。便捷用法见 <see cref="DistributedLockExtensions"/>。
/// </remarks>
public interface IDistributedLock
{
    /// <summary>
    /// 尝试获取锁（单次，未获取到返回 <see langword="null"/>，不阻塞、不重试）。
    /// </summary>
    /// <param name="resourceKey">资源键（同一键全局互斥）</param>
    /// <param name="expiry">锁自动过期时间（防持有者崩溃后死锁）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>锁句柄；获取失败返回 <see langword="null"/></returns>
    Task<IDistributedLockHandle?> TryAcquireAsync(string resourceKey, TimeSpan expiry, CancellationToken cancellationToken = default);
}

/// <summary>
/// 分布式锁句柄。释放锁通过 <see cref="ReleaseAsync"/> 或 <c>Dispose</c>/<c>DisposeAsync</c>（推荐 <c>await using</c>）。
/// </summary>
public interface IDistributedLockHandle : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 资源键。
    /// </summary>
    string ResourceKey { get; }

    /// <summary>
    /// 本次持有的锁标识（释放/续期时校验，避免误删他人锁）。
    /// </summary>
    string LockId { get; }

    /// <summary>
    /// 是否已释放。
    /// </summary>
    bool IsReleased { get; }

    /// <summary>
    /// 释放锁（仅删除自己持有的；幂等）。
    /// </summary>
    Task ReleaseAsync();

    /// <summary>
    /// 续期（仍持有时延长过期时间，用于长任务防锁提前过期）。
    /// </summary>
    /// <param name="expiry">新的过期时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否续期成功（仍持有才成功）</returns>
    Task<bool> ExtendAsync(TimeSpan expiry, CancellationToken cancellationToken = default);
}
