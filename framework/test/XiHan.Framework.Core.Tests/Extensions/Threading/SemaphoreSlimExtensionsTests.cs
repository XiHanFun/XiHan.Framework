// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Extensions.Threading;

namespace XiHan.Framework.Core.Tests.Extensions.Threading;

/// <summary>
/// 信号量扩展方法测试
/// </summary>
/// <remarks>
/// 这组扩展把「等待 + 释放」包成 <c>using</c> 语法，契约有三条：拿到锁后释放必须回到调用方的 <c>Dispose</c>；
/// 超时重载拿不到锁时抛 <see cref="TimeoutException"/>（而不是安静地返回一个假锁）；取消令牌照常生效。
/// 所有用例都用 0 许可或 0 超时构造「立即失败」的场景，不依赖任何真实等待，因此不会拖慢或卡住测试。
/// </remarks>
public class SemaphoreSlimExtensionsTests
{
    /// <summary>
    /// 同步取锁后释放会把许可还回去
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Lock_AcquiresPermitAndReleasesOnDispose()
    {
        using SemaphoreSlim semaphore = new(1, 1);

        using (semaphore.Lock())
        {
            Assert.Equal(0, semaphore.CurrentCount);
        }

        Assert.Equal(1, semaphore.CurrentCount);
    }

    /// <summary>
    /// 异步取锁后释放会把许可还回去
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task LockAsync_AcquiresPermitAndReleasesOnDispose()
    {
        using SemaphoreSlim semaphore = new(1, 1);

        using (await semaphore.LockAsync())
        {
            Assert.Equal(0, semaphore.CurrentCount);
        }

        Assert.Equal(1, semaphore.CurrentCount);
    }

    /// <summary>
    /// 带取消令牌的同步取锁在令牌未取消时正常拿到许可
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Lock_WithLiveToken_AcquiresPermit()
    {
        using SemaphoreSlim semaphore = new(1, 1);

        using (semaphore.Lock(TestContext.Current.CancellationToken))
        {
            Assert.Equal(0, semaphore.CurrentCount);
        }

        Assert.Equal(1, semaphore.CurrentCount);
    }

    /// <summary>
    /// 带取消令牌的异步取锁在令牌未取消时正常拿到许可
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task LockAsync_WithLiveToken_AcquiresPermit()
    {
        using SemaphoreSlim semaphore = new(1, 1);

        using (await semaphore.LockAsync(TestContext.Current.CancellationToken))
        {
            Assert.Equal(0, semaphore.CurrentCount);
        }

        Assert.Equal(1, semaphore.CurrentCount);
    }

    /// <summary>
    /// 四个同步超时重载在有许可时都能立刻拿到锁
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Lock_WithTimeout_AcquiresPermitWhenAvailable()
    {
        using SemaphoreSlim semaphore = new(1, 1);

        using (semaphore.Lock(0))
        {
            Assert.Equal(0, semaphore.CurrentCount);
        }

        using (semaphore.Lock(0, TestContext.Current.CancellationToken))
        {
            Assert.Equal(0, semaphore.CurrentCount);
        }

        using (semaphore.Lock(TimeSpan.Zero))
        {
            Assert.Equal(0, semaphore.CurrentCount);
        }

        using (semaphore.Lock(TimeSpan.Zero, TestContext.Current.CancellationToken))
        {
            Assert.Equal(0, semaphore.CurrentCount);
        }

        Assert.Equal(1, semaphore.CurrentCount);
    }

    /// <summary>
    /// 四个异步超时重载在有许可时都能立刻拿到锁
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task LockAsync_WithTimeout_AcquiresPermitWhenAvailable()
    {
        using SemaphoreSlim semaphore = new(1, 1);

        using (await semaphore.LockAsync(0))
        {
            Assert.Equal(0, semaphore.CurrentCount);
        }

        using (await semaphore.LockAsync(0, TestContext.Current.CancellationToken))
        {
            Assert.Equal(0, semaphore.CurrentCount);
        }

        using (await semaphore.LockAsync(TimeSpan.Zero))
        {
            Assert.Equal(0, semaphore.CurrentCount);
        }

        using (await semaphore.LockAsync(TimeSpan.Zero, TestContext.Current.CancellationToken))
        {
            Assert.Equal(0, semaphore.CurrentCount);
        }

        Assert.Equal(1, semaphore.CurrentCount);
    }

    /// <summary>
    /// 许可耗尽时同步超时重载抛出超时异常
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Lock_WithTimeout_WhenExhausted_ThrowsTimeoutException()
    {
        using SemaphoreSlim semaphore = new(0, 1);

        Assert.Throws<TimeoutException>(() => semaphore.Lock(0));
        Assert.Throws<TimeoutException>(() => semaphore.Lock(0, TestContext.Current.CancellationToken));
        Assert.Throws<TimeoutException>(() => semaphore.Lock(TimeSpan.Zero));
        Assert.Throws<TimeoutException>(() => semaphore.Lock(TimeSpan.Zero, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 许可耗尽时异步超时重载抛出超时异常
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task LockAsync_WithTimeout_WhenExhausted_ThrowsTimeoutException()
    {
        using SemaphoreSlim semaphore = new(0, 1);

        await Assert.ThrowsAsync<TimeoutException>(async () => await semaphore.LockAsync(0));
        await Assert.ThrowsAsync<TimeoutException>(async () => await semaphore.LockAsync(0, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<TimeoutException>(async () => await semaphore.LockAsync(TimeSpan.Zero));
        await Assert.ThrowsAsync<TimeoutException>(async () => await semaphore.LockAsync(TimeSpan.Zero, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 超时失败时不会误释放许可
    /// </summary>
    /// <remarks>
    /// 拿不到锁却把许可 Release 掉是这类包装最典型的写法错误，
    /// 结果是信号量凭空多出许可、并发上限失效，因此单独锁一条。
    /// </remarks>
    [Fact(Timeout = 60_000)]
    public void Lock_WhenTimedOut_DoesNotReleasePermit()
    {
        using SemaphoreSlim semaphore = new(1, 1);
        using var held = semaphore.Lock();

        Assert.Throws<TimeoutException>(() => semaphore.Lock(0));

        Assert.Equal(0, semaphore.CurrentCount);
    }

    /// <summary>
    /// 令牌已取消时同步取锁抛出取消异常
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Lock_WithCancelledToken_ThrowsOperationCanceled()
    {
        using SemaphoreSlim semaphore = new(0, 1);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => semaphore.Lock(cancellation.Token));
        Assert.ThrowsAny<OperationCanceledException>(() => semaphore.Lock(0, cancellation.Token));
    }

    /// <summary>
    /// 令牌已取消时异步取锁抛出取消异常
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task LockAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        using SemaphoreSlim semaphore = new(0, 1);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await semaphore.LockAsync(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await semaphore.LockAsync(0, cancellation.Token));
    }

    /// <summary>
    /// 并发取锁时临界区始终只有一个执行者
    /// </summary>
    /// <remarks>
    /// 用「进入临界区就自增、离开就自减」的并发计数器做见证：只要包装漏放或早放，
    /// 计数器峰值必然大于 1。不使用任何真实等待，靠足够多的轮次制造竞争。
    /// </remarks>
    [Fact(Timeout = 60_000)]
    public async Task Lock_UnderConcurrency_AdmitsOneHolderAtATime()
    {
        using SemaphoreSlim semaphore = new(1, 1);
        var concurrent = 0;
        var peak = 0;
        var completed = 0;

        var workers = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            for (var round = 0; round < 50; round++)
            {
                using (await semaphore.LockAsync())
                {
                    var current = Interlocked.Increment(ref concurrent);
                    InterlockedMax(ref peak, current);
                    Interlocked.Increment(ref completed);
                    Interlocked.Decrement(ref concurrent);
                }
            }
        }, TestContext.Current.CancellationToken)).ToArray();

        await Task.WhenAll(workers);

        Assert.Equal(1, peak);
        Assert.Equal(8 * 50, completed);
        Assert.Equal(1, semaphore.CurrentCount);
    }

    /// <summary>
    /// 以原子方式把目标更新为更大的值
    /// </summary>
    /// <param name="target">目标变量</param>
    /// <param name="candidate">候选值</param>
    private static void InterlockedMax(ref int target, int candidate)
    {
        var snapshot = Volatile.Read(ref target);
        while (candidate > snapshot)
        {
            var previous = Interlocked.CompareExchange(ref target, candidate, snapshot);
            if (previous == snapshot)
            {
                return;
            }

            snapshot = previous;
        }
    }
}
