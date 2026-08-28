// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Caching.Distributed.Abstracts;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;

/// <summary>
/// 分布式锁替身
/// </summary>
/// <remarks>
/// Worker 的"多实例单活"依赖抢锁结果：抢不到必须整轮跳过、连存储都不能碰。
/// 本替身用 <see cref="CanAcquire"/> 直接控制抢锁成败，并记录锁名与 TTL 供断言。
/// </remarks>
public sealed class FakeDistributedLock : IDistributedLock
{
    private readonly object _gate = new();
    private int _acquireCount;
    private int _releasedCount;
    private string? _lastResourceKey;
    private TimeSpan? _lastExpiry;

    /// <summary>
    /// 是否允许抢到锁
    /// </summary>
    public bool CanAcquire { get; set; } = true;

    /// <summary>
    /// 抢锁次数
    /// </summary>
    public int AcquireCount
    {
        get
        {
            lock (_gate)
            {
                return _acquireCount;
            }
        }
    }

    /// <summary>
    /// 锁释放次数
    /// </summary>
    public int ReleasedCount
    {
        get
        {
            lock (_gate)
            {
                return _releasedCount;
            }
        }
    }

    /// <summary>
    /// 最近一次抢锁使用的资源键
    /// </summary>
    public string? LastResourceKey
    {
        get
        {
            lock (_gate)
            {
                return _lastResourceKey;
            }
        }
    }

    /// <summary>
    /// 最近一次抢锁使用的过期时间
    /// </summary>
    public TimeSpan? LastExpiry
    {
        get
        {
            lock (_gate)
            {
                return _lastExpiry;
            }
        }
    }

    /// <summary>
    /// 尝试获取锁
    /// </summary>
    /// <param name="resourceKey">资源键</param>
    /// <param name="expiry">过期时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>锁句柄，抢不到为 null</returns>
    public Task<IDistributedLockHandle?> TryAcquireAsync(string resourceKey, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _acquireCount++;
            _lastResourceKey = resourceKey;
            _lastExpiry = expiry;
        }

        return CanAcquire
            ? Task.FromResult<IDistributedLockHandle?>(new FakeDistributedLockHandle(this, resourceKey))
            : Task.FromResult<IDistributedLockHandle?>(null);
    }

    /// <summary>
    /// 记录一次释放
    /// </summary>
    internal void MarkReleased()
    {
        lock (_gate)
        {
            _releasedCount++;
        }
    }
}

/// <summary>
/// 分布式锁句柄替身
/// </summary>
public sealed class FakeDistributedLockHandle : IDistributedLockHandle
{
    private readonly FakeDistributedLock _owner;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="owner">所属锁</param>
    /// <param name="resourceKey">资源键</param>
    public FakeDistributedLockHandle(FakeDistributedLock owner, string resourceKey)
    {
        _owner = owner;
        ResourceKey = resourceKey;
    }

    /// <summary>
    /// 资源键
    /// </summary>
    public string ResourceKey { get; }

    /// <summary>
    /// 锁标识
    /// </summary>
    public string LockId { get; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 是否已释放
    /// </summary>
    public bool IsReleased { get; private set; }

    /// <summary>
    /// 释放锁
    /// </summary>
    public void Dispose()
    {
        Release();
    }

    /// <summary>
    /// 异步释放锁
    /// </summary>
    /// <returns>值任务</returns>
    public ValueTask DisposeAsync()
    {
        Release();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 释放锁
    /// </summary>
    /// <returns>任务</returns>
    public Task ReleaseAsync()
    {
        Release();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 续期
    /// </summary>
    /// <param name="expiry">新的过期时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否续期成功</returns>
    public Task<bool> ExtendAsync(TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(!IsReleased);
    }

    private void Release()
    {
        if (IsReleased)
        {
            return;
        }

        IsReleased = true;
        _owner.MarkReleased();
    }
}
