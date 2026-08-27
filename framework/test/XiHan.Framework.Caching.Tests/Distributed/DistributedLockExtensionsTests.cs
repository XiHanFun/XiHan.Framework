// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Caching.Distributed;
using XiHan.Framework.Caching.Distributed.Abstracts;

namespace XiHan.Framework.Caching.Tests;

/// <summary>
/// 分布式锁便捷扩展测试
/// </summary>
/// <remarks>
/// 扩展方法的价值在于「拿不到锁就安全跳过」和「无论成败都释放」，用进程内回退实现驱动即可覆盖这两点。
/// </remarks>
public class DistributedLockExtensionsTests
{
    private static readonly TimeSpan LongExpiry = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 拿到锁时执行临界区并返回成功
    /// </summary>
    [Fact]
    public async Task WithLockAsync_WhenAcquired_RunsActionAndReturnsTrue()
    {
        var token = TestContext.Current.CancellationToken;
        var distributedLock = new InMemoryDistributedLock();
        var executed = false;

        var acquired = await distributedLock.WithLockAsync("resource", LongExpiry, _ =>
        {
            executed = true;
            return Task.CompletedTask;
        }, token);

        Assert.True(acquired);
        Assert.True(executed);
    }

    /// <summary>
    /// 执行完临界区后锁被释放
    /// </summary>
    [Fact]
    public async Task WithLockAsync_AfterAction_ReleasesLock()
    {
        var token = TestContext.Current.CancellationToken;
        var distributedLock = new InMemoryDistributedLock();

        await distributedLock.WithLockAsync("resource", LongExpiry, _ => Task.CompletedTask, token);

        await using var handle = await distributedLock.TryAcquireAsync("resource", LongExpiry, token);

        Assert.NotNull(handle);
    }

    /// <summary>
    /// 临界区抛异常时锁仍被释放
    /// </summary>
    /// <remarks>
    /// 释放走的是 await using，异常路径不释放会把资源锁死到过期为止。
    /// </remarks>
    [Fact]
    public async Task WithLockAsync_WhenActionThrows_StillReleasesLock()
    {
        var token = TestContext.Current.CancellationToken;
        var distributedLock = new InMemoryDistributedLock();

        await Assert.ThrowsAsync<InvalidOperationException>(() => distributedLock.WithLockAsync(
            "resource",
            LongExpiry,
            _ => throw new InvalidOperationException("临界区失败"),
            token));

        await using var handle = await distributedLock.TryAcquireAsync("resource", LongExpiry, token);

        Assert.NotNull(handle);
    }

    /// <summary>
    /// 拿不到锁时跳过临界区并返回失败
    /// </summary>
    [Fact]
    public async Task WithLockAsync_WhenNotAcquired_SkipsActionAndReturnsFalse()
    {
        var token = TestContext.Current.CancellationToken;
        var distributedLock = new InMemoryDistributedLock();
        await using var holder = await distributedLock.TryAcquireAsync("resource", LongExpiry, token);
        Assert.NotNull(holder);
        var executed = false;

        var acquired = await distributedLock.WithLockAsync("resource", LongExpiry, _ =>
        {
            executed = true;
            return Task.CompletedTask;
        }, token);

        Assert.False(acquired);
        Assert.False(executed);
    }

    /// <summary>
    /// 带返回值版本拿到锁时返回执行结果
    /// </summary>
    [Fact]
    public async Task WithLockAsync_WithResult_WhenAcquired_ReturnsValue()
    {
        var token = TestContext.Current.CancellationToken;
        var distributedLock = new InMemoryDistributedLock();

        var (acquired, result) = await distributedLock.WithLockAsync("resource", LongExpiry, _ => Task.FromResult(7), token);

        Assert.True(acquired);
        Assert.Equal(7, result);
    }

    /// <summary>
    /// 带返回值版本拿不到锁时返回默认值
    /// </summary>
    [Fact]
    public async Task WithLockAsync_WithResult_WhenNotAcquired_ReturnsDefault()
    {
        var token = TestContext.Current.CancellationToken;
        var distributedLock = new InMemoryDistributedLock();
        await using var holder = await distributedLock.TryAcquireAsync("resource", LongExpiry, token);
        Assert.NotNull(holder);
        var executed = false;

        var (acquired, result) = await distributedLock.WithLockAsync("resource", LongExpiry, _ =>
        {
            executed = true;
            return Task.FromResult(7);
        }, token);

        Assert.False(acquired);
        Assert.Equal(0, result);
        Assert.False(executed);
    }

    /// <summary>
    /// 等待获取在资源空闲时立即返回句柄
    /// </summary>
    [Fact]
    public async Task AcquireAsync_WhenFree_ReturnsHandle()
    {
        var token = TestContext.Current.CancellationToken;
        var distributedLock = new InMemoryDistributedLock();

        await using var handle = await distributedLock.AcquireAsync("resource", LongExpiry, TimeSpan.FromSeconds(1), cancellationToken: token);

        Assert.NotNull(handle);
    }

    /// <summary>
    /// 等待时长为零且资源被占用时立即返回空
    /// </summary>
    /// <remarks>
    /// 等待时长为零应当只试一次就放弃，不能再睡一个轮询周期，否则调用方的超时预算会被吃掉。
    /// </remarks>
    [Fact(Timeout = 60_000)]
    public async Task AcquireAsync_WithZeroWaitAndHeldResource_ReturnsNull()
    {
        var token = TestContext.Current.CancellationToken;
        var distributedLock = new InMemoryDistributedLock();
        await using var holder = await distributedLock.TryAcquireAsync("resource", LongExpiry, token);
        Assert.NotNull(holder);

        var handle = await distributedLock.AcquireAsync("resource", LongExpiry, TimeSpan.Zero, cancellationToken: token);

        Assert.Null(handle);
    }

    /// <summary>
    /// 锁实例为空时拒绝执行
    /// </summary>
    [Fact]
    public async Task WithLockAsync_WithNullLock_Throws()
    {
        IDistributedLock? distributedLock = null;

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => distributedLock!.WithLockAsync("resource", LongExpiry, _ => Task.CompletedTask));
    }

    /// <summary>
    /// 临界区委托为空时拒绝执行
    /// </summary>
    [Fact]
    public async Task WithLockAsync_WithNullAction_Throws()
    {
        var distributedLock = new InMemoryDistributedLock();
        Func<CancellationToken, Task>? action = null;

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => distributedLock.WithLockAsync("resource", LongExpiry, action!));
    }

    /// <summary>
    /// 等待获取时锁实例为空则拒绝执行
    /// </summary>
    [Fact]
    public async Task AcquireAsync_WithNullLock_Throws()
    {
        IDistributedLock? distributedLock = null;

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => distributedLock!.AcquireAsync("resource", LongExpiry, TimeSpan.Zero));
    }
}
