// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Threading;

namespace XiHan.Framework.Utils.Tests.Threading;

/// <summary>
/// 异步读写锁测试
/// </summary>
/// <remarks>
/// 重点锁住"读锁获取超时后锁状态仍然自洽"这条：
/// 原实现在 _readerCount 自增之后才去等写信号量，超时抛 TimeoutException 时
/// finally 只放掉了 _readerSemaphore，_readerCount 永久停在非零值，
/// 之后每个读者都看到计数不为 1、再也不会去占写信号量，
/// 读锁与写锁从此可以同时持有，互斥彻底失效且无法自愈。
/// </remarks>
public class AsyncReaderWriterLockTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan LongTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 写锁被占时带超时获取读锁会抛超时异常
    /// </summary>
    [Fact]
    public async Task AcquireReadLockAsync_WhenWriteLockHeld_TimesOut()
    {
        var rwLock = new AsyncReaderWriterLock();
        using var writeLock = await rwLock.AcquireWriteLockAsync();

        await Assert.ThrowsAsync<TimeoutException>(() => rwLock.AcquireReadLockAsync(ShortTimeout));
    }

    /// <summary>
    /// 读锁获取超时后不污染读者计数，读写互斥依然生效
    /// </summary>
    [Fact]
    public async Task AcquireReadLockAsync_AfterTimeout_StillBlocksWriterWhileReaderHeld()
    {
        var rwLock = new AsyncReaderWriterLock();

        // 先制造一次读锁超时
        var writeLock = await rwLock.AcquireWriteLockAsync();
        await Assert.ThrowsAsync<TimeoutException>(() => rwLock.AcquireReadLockAsync(ShortTimeout));
        writeLock.Dispose();

        // 超时那次必须完全回滚：此时拿到的读锁应当是"第一个读者"，要能挡住写者
        using (await rwLock.AcquireReadLockAsync(LongTimeout))
        {
            await Assert.ThrowsAsync<TimeoutException>(() => rwLock.AcquireWriteLockAsync(ShortTimeout));
        }

        // 读锁释放后写者可以正常进入
        using var writeLockAgain = await rwLock.AcquireWriteLockAsync(LongTimeout);
        Assert.NotNull(writeLockAgain);
    }

    /// <summary>
    /// 连续多次读锁超时后锁仍可正常使用
    /// </summary>
    [Fact]
    public async Task AcquireReadLockAsync_AfterRepeatedTimeouts_StaysConsistent()
    {
        var rwLock = new AsyncReaderWriterLock();
        var writeLock = await rwLock.AcquireWriteLockAsync();

        for (var i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<TimeoutException>(() => rwLock.AcquireReadLockAsync(ShortTimeout));
        }

        writeLock.Dispose();

        using (await rwLock.AcquireReadLockAsync(LongTimeout))
        {
            await Assert.ThrowsAsync<TimeoutException>(() => rwLock.AcquireWriteLockAsync(ShortTimeout));
        }

        using var writeLockAgain = await rwLock.AcquireWriteLockAsync(LongTimeout);
        Assert.NotNull(writeLockAgain);
    }

    /// <summary>
    /// 多个读者可以同时持有读锁，全部释放后写者才能进入
    /// </summary>
    [Fact]
    public async Task AcquireReadLockAsync_AllowsConcurrentReadersAndBlocksWriterUntilAllReleased()
    {
        var rwLock = new AsyncReaderWriterLock();

        var firstReader = await rwLock.AcquireReadLockAsync(LongTimeout);
        var secondReader = await rwLock.AcquireReadLockAsync(LongTimeout);

        await Assert.ThrowsAsync<TimeoutException>(() => rwLock.AcquireWriteLockAsync(ShortTimeout));

        firstReader.Dispose();
        await Assert.ThrowsAsync<TimeoutException>(() => rwLock.AcquireWriteLockAsync(ShortTimeout));

        secondReader.Dispose();
        using var writeLock = await rwLock.AcquireWriteLockAsync(LongTimeout);
        Assert.NotNull(writeLock);
    }

    /// <summary>
    /// 写锁之间互斥，释放后下一个写者才能进入
    /// </summary>
    [Fact]
    public async Task AcquireWriteLockAsync_IsMutuallyExclusive()
    {
        var rwLock = new AsyncReaderWriterLock();

        var writeLock = await rwLock.AcquireWriteLockAsync(LongTimeout);
        await Assert.ThrowsAsync<TimeoutException>(() => rwLock.AcquireWriteLockAsync(ShortTimeout));

        writeLock.Dispose();

        using var nextWriteLock = await rwLock.AcquireWriteLockAsync(LongTimeout);
        Assert.NotNull(nextWriteLock);
    }

    /// <summary>
    /// 释放器重复释放不会把读者计数减穿
    /// </summary>
    [Fact]
    public async Task Releaser_WhenDisposedTwice_ReleasesOnce()
    {
        var rwLock = new AsyncReaderWriterLock();

        var readLock = await rwLock.AcquireReadLockAsync(LongTimeout);
        readLock.Dispose();
        readLock.Dispose();

        using var writeLock = await rwLock.AcquireWriteLockAsync(LongTimeout);
        Assert.NotNull(writeLock);
    }

    /// <summary>
    /// 并发读写压力下锁不会把两类持有者同时放进临界区
    /// </summary>
    [Fact]
    public async Task ReadersAndWriters_UnderContention_NeverOverlap()
    {
        var rwLock = new AsyncReaderWriterLock();
        var activeReaders = 0;
        var activeWriters = 0;
        var violations = 0;

        async Task ReadAsync()
        {
            for (var i = 0; i < 20; i++)
            {
                using (await rwLock.AcquireReadLockAsync())
                {
                    Interlocked.Increment(ref activeReaders);
                    if (Volatile.Read(ref activeWriters) > 0)
                    {
                        Interlocked.Increment(ref violations);
                    }

                    await Task.Yield();
                    Interlocked.Decrement(ref activeReaders);
                }
            }
        }

        async Task WriteAsync()
        {
            for (var i = 0; i < 20; i++)
            {
                using (await rwLock.AcquireWriteLockAsync())
                {
                    Interlocked.Increment(ref activeWriters);
                    if (Volatile.Read(ref activeReaders) > 0 || Volatile.Read(ref activeWriters) > 1)
                    {
                        Interlocked.Increment(ref violations);
                    }

                    await Task.Yield();
                    Interlocked.Decrement(ref activeWriters);
                }
            }
        }

        // 互斥一旦失效通常表现为死等，这里给个上限让它失败而不是挂住
        await Task.WhenAll(ReadAsync(), ReadAsync(), ReadAsync(), WriteAsync(), WriteAsync())
            .WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(0, violations);
    }
}
