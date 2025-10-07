#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IJobLockProvider
// Guid:772cef3c-140b-4007-9f7c-d0a22d3fd6e9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 17:09:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Tasks.ScheduledJobs.Abstractions;

/// <summary>
/// 任务分布式锁提供者接口
/// </summary>
public interface IJobLockProvider
{
    /// <summary>
    /// 尝试获取锁
    /// </summary>
    /// <param name="resourceKey">资源键</param>
    /// <param name="expiry">过期时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>锁令牌，null表示获取失败</returns>
    Task<ILockToken?> TryAcquireLockAsync(string resourceKey, TimeSpan expiry, CancellationToken cancellationToken = default);
}

/// <summary>
/// 锁令牌接口
/// </summary>
public interface ILockToken : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 资源键
    /// </summary>
    string ResourceKey { get; }

    /// <summary>
    /// 锁标识
    /// </summary>
    string LockId { get; }

    /// <summary>
    /// 是否已释放
    /// </summary>
    bool IsReleased { get; }

    /// <summary>
    /// 释放锁
    /// </summary>
    Task ReleaseAsync();
}
