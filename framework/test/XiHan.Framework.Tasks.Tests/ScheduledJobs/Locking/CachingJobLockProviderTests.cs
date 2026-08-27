// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Caching.Distributed.Abstracts;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Locking;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Locking;

/// <summary>
/// CachingJobLockProvider 分布式锁适配器测试
/// </summary>
/// <remarks>
/// 适配器把 Caching 模块的分布式锁句柄包装成任务侧的锁令牌，全部行为都必须是纯转发。
/// 用手写的假分布式锁验证，不连 Redis。
/// </remarks>
public class CachingJobLockProviderTests
{
    /// <summary>
    /// 加锁请求的资源键、过期时间与取消令牌原样转发给底层分布式锁
    /// </summary>
    [Fact]
    public async Task TryAcquireLockAsync_ForwardsArgumentsToDistributedLock()
    {
        var distributedLock = new StubDistributedLock(new StubLockHandle("job:lock:a"));
        var provider = new CachingJobLockProvider(distributedLock);
        using var cts = new CancellationTokenSource();

        await provider.TryAcquireLockAsync("job:lock:a", TimeSpan.FromSeconds(30), cts.Token);

        Assert.Equal("job:lock:a", distributedLock.LastResourceKey);
        Assert.Equal(TimeSpan.FromSeconds(30), distributedLock.LastExpiry);
        Assert.Equal(cts.Token, distributedLock.LastCancellationToken);
        Assert.Equal(1, distributedLock.AcquireAttempts);
    }

    /// <summary>
    /// 底层拿不到锁时返回 null，而不是返回一个假的已释放令牌
    /// </summary>
    [Fact]
    public async Task TryAcquireLockAsync_WhenHandleIsNull_ReturnsNull()
    {
        var provider = new CachingJobLockProvider(new StubDistributedLock(null));

        var token = await provider.TryAcquireLockAsync("job:lock:a", TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Null(token);
    }

    /// <summary>
    /// 拿到锁时返回包装令牌，资源键与锁标识透传自底层句柄
    /// </summary>
    [Fact]
    public async Task TryAcquireLockAsync_WhenHandleAcquired_WrapsItIntoLockToken()
    {
        var handle = new StubLockHandle("job:lock:a");
        var provider = new CachingJobLockProvider(new StubDistributedLock(handle));

        var token = await provider.TryAcquireLockAsync("job:lock:a", TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.NotNull(token);
        Assert.Equal(handle.ResourceKey, token!.ResourceKey);
        Assert.Equal(handle.LockId, token.LockId);
        Assert.False(token.IsReleased);
    }

    /// <summary>
    /// 显式释放转发到底层句柄，且释放状态随之变化
    /// </summary>
    [Fact]
    public async Task ReleaseAsync_ForwardsToHandleAndFlipsReleasedFlag()
    {
        var handle = new StubLockHandle("job:lock:a");
        var provider = new CachingJobLockProvider(new StubDistributedLock(handle));

        var token = await provider.TryAcquireLockAsync("job:lock:a", TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await token!.ReleaseAsync();

        Assert.Equal(1, handle.ReleaseCount);
        Assert.True(handle.IsReleased);
        Assert.True(token.IsReleased);
    }

    /// <summary>
    /// 同步释放转发到底层句柄
    /// </summary>
    [Fact]
    public async Task Dispose_ForwardsToHandle()
    {
        var handle = new StubLockHandle("job:lock:a");
        var provider = new CachingJobLockProvider(new StubDistributedLock(handle));

        var token = await provider.TryAcquireLockAsync("job:lock:a", TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        token!.Dispose();

        Assert.Equal(1, handle.DisposeCount);
        Assert.True(token.IsReleased);
    }

    /// <summary>
    /// 异步释放转发到底层句柄
    /// </summary>
    [Fact]
    public async Task DisposeAsync_ForwardsToHandle()
    {
        var handle = new StubLockHandle("job:lock:a");
        var provider = new CachingJobLockProvider(new StubDistributedLock(handle));

        var token = await provider.TryAcquireLockAsync("job:lock:a", TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await token!.DisposeAsync();

        Assert.Equal(1, handle.AsyncDisposeCount);
        Assert.True(token.IsReleased);
    }

    /// <summary>
    /// 每次加锁都返回独立的令牌实例，包装同一次句柄不会串号
    /// </summary>
    [Fact]
    public async Task TryAcquireLockAsync_CalledTwice_ReturnsDistinctTokens()
    {
        var provider = new CachingJobLockProvider(new StubDistributedLock(new StubLockHandle("job:lock:a")));

        var first = await provider.TryAcquireLockAsync("job:lock:a", TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        var second = await provider.TryAcquireLockAsync("job:lock:a", TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
    }

    /// <summary>
    /// 适配器实现的是任务侧的锁提供者接口，可直接注册到管道
    /// </summary>
    [Fact]
    public void Instance_ImplementsJobLockProvider()
    {
        var provider = new CachingJobLockProvider(new StubDistributedLock(null));

        Assert.IsAssignableFrom<IJobLockProvider>(provider);
    }

    /// <summary>
    /// 可预置返回句柄的假分布式锁
    /// </summary>
    private sealed class StubDistributedLock : IDistributedLock
    {
        private readonly IDistributedLockHandle? _handle;

        /// <summary>
        /// 构造函数
        /// </summary>
        public StubDistributedLock(IDistributedLockHandle? handle)
        {
            _handle = handle;
        }

        /// <summary>
        /// 加锁尝试次数
        /// </summary>
        public int AcquireAttempts { get; private set; }

        /// <summary>
        /// 最近一次的资源键
        /// </summary>
        public string? LastResourceKey { get; private set; }

        /// <summary>
        /// 最近一次的过期时间
        /// </summary>
        public TimeSpan LastExpiry { get; private set; }

        /// <summary>
        /// 最近一次的取消令牌
        /// </summary>
        public CancellationToken LastCancellationToken { get; private set; }

        /// <summary>
        /// 尝试获取锁
        /// </summary>
        public Task<IDistributedLockHandle?> TryAcquireAsync(string resourceKey, TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            AcquireAttempts++;
            LastResourceKey = resourceKey;
            LastExpiry = expiry;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(_handle);
        }
    }

    /// <summary>
    /// 记录释放行为的假锁句柄
    /// </summary>
    private sealed class StubLockHandle : IDistributedLockHandle
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        public StubLockHandle(string resourceKey)
        {
            ResourceKey = resourceKey;
        }

        /// <summary>
        /// 同步释放次数
        /// </summary>
        public int DisposeCount { get; private set; }

        /// <summary>
        /// 异步释放次数
        /// </summary>
        public int AsyncDisposeCount { get; private set; }

        /// <summary>
        /// 显式释放次数
        /// </summary>
        public int ReleaseCount { get; private set; }

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
        public Task ReleaseAsync()
        {
            ReleaseCount++;
            IsReleased = true;
            return Task.CompletedTask;
        }

        /// <summary>
        /// 续期
        /// </summary>
        public Task<bool> ExtendAsync(TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(!IsReleased);
        }

        /// <summary>
        /// 同步释放
        /// </summary>
        public void Dispose()
        {
            DisposeCount++;
            IsReleased = true;
        }

        /// <summary>
        /// 异步释放
        /// </summary>
        public ValueTask DisposeAsync()
        {
            AsyncDisposeCount++;
            IsReleased = true;
            return ValueTask.CompletedTask;
        }
    }
}
