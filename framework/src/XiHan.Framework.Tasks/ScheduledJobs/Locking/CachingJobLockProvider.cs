// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Caching.Distributed.Abstracts;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;

namespace XiHan.Framework.Tasks.ScheduledJobs.Locking;

/// <summary>
/// 任务分布式锁提供者：适配到框架统一的分布式锁 <see cref="IDistributedLock"/>（Caching 模块）。
/// </summary>
/// <remarks>
/// 不再自带 Redis/内存锁实现 —— 实际是 Redis 跨实例锁还是进程内回退，由 <see cref="IDistributedLock"/>
/// 按 Redis 配置自动选择（启用 Redis → 跨实例；否则 → 进程内）。
/// </remarks>
public sealed class CachingJobLockProvider : IJobLockProvider
{
    private readonly IDistributedLock _distributedLock;

    /// <summary>
    /// 构造函数
    /// </summary>
    public CachingJobLockProvider(IDistributedLock distributedLock)
    {
        _distributedLock = distributedLock;
    }

    /// <inheritdoc />
    public async Task<ILockToken?> TryAcquireLockAsync(string resourceKey, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var handle = await _distributedLock.TryAcquireAsync(resourceKey, expiry, cancellationToken);
        return handle is null ? null : new CachingJobLockToken(handle);
    }
}

/// <summary>
/// <see cref="ILockToken"/> 适配器：包装 <see cref="IDistributedLockHandle"/>。
/// </summary>
internal sealed class CachingJobLockToken : ILockToken
{
    private readonly IDistributedLockHandle _handle;

    public CachingJobLockToken(IDistributedLockHandle handle)
    {
        _handle = handle;
    }

    public string ResourceKey => _handle.ResourceKey;

    public string LockId => _handle.LockId;

    public bool IsReleased => _handle.IsReleased;

    public Task ReleaseAsync()
    {
        return _handle.ReleaseAsync();
    }

    public void Dispose()
    {
        _handle.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _handle.DisposeAsync();
    }
}
