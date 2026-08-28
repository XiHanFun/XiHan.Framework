// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Utils.Tests.Extensions;

/// <summary>
/// 锁扩展方法测试
/// </summary>
/// <remarks>
/// Action 重载统一用语句块 lambda 书写：表达式 lambda 同时可转成 Action 与 Func&lt;T&gt;，会造成重载歧义。
/// 并发用例只跑有界的固定次数，不依赖真实等待。
/// </remarks>
public class LockExtensionsTests
{
    /// <summary>
    /// 加锁执行操作
    /// </summary>
    [Fact]
    public void Lock_WithAction_ExecutesBody()
    {
        var lockObj = new object();
        var executed = false;

        lockObj.Lock(() =>
        {
            executed = true;
        });

        Assert.True(executed);
    }

    /// <summary>
    /// 加锁执行并返回结果
    /// </summary>
    [Fact]
    public void Lock_WithFunc_ReturnsResult()
    {
        var lockObj = new object();

        var result = lockObj.Lock(() => 42);

        Assert.Equal(42, result);
    }

    /// <summary>
    /// 锁对象或委托为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void Lock_WhenArgumentIsNull_Throws()
    {
        var lockObj = new object();

        Assert.Throws<ArgumentNullException>(() => LockExtensions.Lock(null!, () => { }));
        Assert.Throws<ArgumentNullException>(() => lockObj.Lock((Action)null!));
        Assert.Throws<ArgumentNullException>(() =>
        {
            lockObj.Lock((Func<int>)null!);
        });
    }

    /// <summary>
    /// 并发累加在锁保护下不丢更新
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Lock_UnderConcurrency_KeepsCounterConsistent()
    {
        var lockObj = new object();
        var counter = 0;

        Parallel.For(0, 2000, _ => lockObj.Lock(() =>
        {
            counter++;
        }));

        Assert.Equal(2000, counter);
    }

    /// <summary>
    /// 读锁执行操作并正确释放
    /// </summary>
    [Fact]
    public void ReadLock_ExecutesAndReleases()
    {
        using var lockSlim = new ReaderWriterLockSlim();
        var executed = false;

        lockSlim.ReadLock(() =>
        {
            executed = true;
        });

        Assert.True(executed);
        Assert.False(lockSlim.IsReadLockHeld);
    }

    /// <summary>
    /// 读锁执行并返回结果，即使回调抛异常也会释放
    /// </summary>
    [Fact]
    public void ReadLock_WithFunc_ReturnsResultAndReleasesOnThrow()
    {
        using var lockSlim = new ReaderWriterLockSlim();

        Assert.Equal("ok", lockSlim.ReadLock(() => "ok"));

        Assert.Throws<InvalidOperationException>(() =>
        {
            lockSlim.ReadLock<string>(() => throw new InvalidOperationException());
        });
        Assert.False(lockSlim.IsReadLockHeld);
    }

    /// <summary>
    /// 写锁执行操作并正确释放
    /// </summary>
    [Fact]
    public void WriteLock_ExecutesAndReleases()
    {
        using var lockSlim = new ReaderWriterLockSlim();
        var executed = false;

        lockSlim.WriteLock(() =>
        {
            executed = true;
        });

        Assert.True(executed);
        Assert.False(lockSlim.IsWriteLockHeld);
    }

    /// <summary>
    /// 写锁执行并返回结果，即使回调抛异常也会释放
    /// </summary>
    [Fact]
    public void WriteLock_WithFunc_ReturnsResultAndReleasesOnThrow()
    {
        using var lockSlim = new ReaderWriterLockSlim();

        Assert.Equal(7, lockSlim.WriteLock(() => 7));

        Assert.Throws<InvalidOperationException>(() =>
        {
            lockSlim.WriteLock<int>(() => throw new InvalidOperationException());
        });
        Assert.False(lockSlim.IsWriteLockHeld);
    }

    /// <summary>
    /// 读写锁扩展在委托为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void ReaderWriterLockExtensions_WhenArgumentIsNull_Throw()
    {
        using var lockSlim = new ReaderWriterLockSlim();

        Assert.Throws<ArgumentNullException>(() => LockExtensions.ReadLock(null!, () => { }));
        Assert.Throws<ArgumentNullException>(() => lockSlim.ReadLock((Action)null!));
        Assert.Throws<ArgumentNullException>(() => LockExtensions.WriteLock(null!, () => { }));
        Assert.Throws<ArgumentNullException>(() => lockSlim.WriteLock((Action)null!));
    }

    /// <summary>
    /// 无竞争时超时锁能立即拿到并执行
    /// </summary>
    [Fact]
    public void TryLock_WhenUncontended_RunsBodyAndReturnsTrue()
    {
        var lockObj = new object();
        var executed = false;

        var acquired = lockObj.TryLock(TimeSpan.FromMilliseconds(50), () =>
        {
            executed = true;
        });

        Assert.True(acquired);
        Assert.True(executed);
    }

    /// <summary>
    /// 无竞争时超时锁返回成功标记与结果
    /// </summary>
    [Fact]
    public void TryLock_WithFunc_ReturnsSuccessAndResult()
    {
        var lockObj = new object();

        var (success, result) = lockObj.TryLock(TimeSpan.FromMilliseconds(50), () => 5);

        Assert.True(success);
        Assert.Equal(5, result);
    }

    /// <summary>
    /// 锁对象或委托为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void TryLock_WhenArgumentIsNull_Throws()
    {
        var lockObj = new object();
        var timeout = TimeSpan.FromMilliseconds(1);

        Assert.Throws<ArgumentNullException>(() =>
        {
            LockExtensions.TryLock(null!, timeout, () => { });
        });
        Assert.Throws<ArgumentNullException>(() =>
        {
            lockObj.TryLock(timeout, (Action)null!);
        });
        Assert.Throws<ArgumentNullException>(() =>
        {
            lockObj.TryLock(timeout, (Func<int>)null!);
        });
    }
}
