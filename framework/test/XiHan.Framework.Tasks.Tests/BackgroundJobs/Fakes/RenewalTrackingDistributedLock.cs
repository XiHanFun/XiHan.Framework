// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Caching.Distributed.Abstracts;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;

/// <summary>
/// 记录续期行为的分布式锁替身
/// </summary>
/// <remarks>
/// 与 <see cref="FakeDistributedLock"/> 只关心"抢得到抢不到"不同，这个替身盯的是锁被持有之后的事：
/// 长轮次里有没有按周期续期、续不上时调用方是不是及时收手。
/// <see cref="CanExtend"/> 用来编排续期失败——真实场景下这意味着锁已过期并被另一实例抢走，
/// 此时继续跑剩下的作业就会与那个实例重复执行。
/// </remarks>
public sealed class RenewalTrackingDistributedLock : IDistributedLock
{
    private readonly object _gate = new();
    private int _acquireCount;
    private int _extendCallCount;
    private TimeSpan? _lastExtendExpiry;

    /// <summary>
    /// 续期是否成功（false 表示锁已不再由自己持有）
    /// </summary>
    public bool CanExtend { get; set; } = true;

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
    /// 续期调用次数
    /// </summary>
    public int ExtendCallCount
    {
        get
        {
            lock (_gate)
            {
                return _extendCallCount;
            }
        }
    }

    /// <summary>
    /// 最近一次续期请求的过期时间
    /// </summary>
    public TimeSpan? LastExtendExpiry
    {
        get
        {
            lock (_gate)
            {
                return _lastExtendExpiry;
            }
        }
    }

    /// <summary>
    /// 尝试获取锁（本替身总是能抢到）
    /// </summary>
    /// <param name="resourceKey">资源键</param>
    /// <param name="expiry">过期时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>锁句柄</returns>
    public Task<IDistributedLockHandle?> TryAcquireAsync(string resourceKey, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _acquireCount++;
        }

        return Task.FromResult<IDistributedLockHandle?>(new RenewalTrackingDistributedLockHandle(this, resourceKey));
    }

    /// <summary>
    /// 记录一次续期请求并给出续期结果
    /// </summary>
    /// <param name="expiry">续期请求的过期时间</param>
    /// <returns>是否续期成功</returns>
    internal bool RecordExtend(TimeSpan expiry)
    {
        lock (_gate)
        {
            _extendCallCount++;
            _lastExtendExpiry = expiry;
            return CanExtend;
        }
    }
}

/// <summary>
/// 记录续期行为的分布式锁句柄替身
/// </summary>
public sealed class RenewalTrackingDistributedLockHandle : IDistributedLockHandle
{
    private readonly RenewalTrackingDistributedLock _owner;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="owner">所属锁</param>
    /// <param name="resourceKey">资源键</param>
    public RenewalTrackingDistributedLockHandle(RenewalTrackingDistributedLock owner, string resourceKey)
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
        IsReleased = true;
    }

    /// <summary>
    /// 异步释放锁
    /// </summary>
    /// <returns>值任务</returns>
    public ValueTask DisposeAsync()
    {
        IsReleased = true;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 释放锁
    /// </summary>
    /// <returns>任务</returns>
    public Task ReleaseAsync()
    {
        IsReleased = true;
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
        return Task.FromResult(_owner.RecordExtend(expiry));
    }
}
