// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;

namespace XiHan.Framework.Utils.Threading;

/// <summary>
/// 异步读写锁
/// </summary>
public class AsyncReaderWriterLock
{
    private readonly SemaphoreSlim _readerSemaphore = new(1, 1);
    private readonly SemaphoreSlim _writerSemaphore = new(1, 1);
    private int _readerCount;

    /// <summary>
    /// 初始化异步读写锁
    /// </summary>
    public AsyncReaderWriterLock()
    {
    }

    #region 读锁

    /// <summary>
    /// 异步获取读锁
    /// </summary>
    /// <returns></returns>
    public async Task<IDisposable> AcquireReadLockAsync()
    {
        await _readerSemaphore.WaitAsync();
        try
        {
            if (++_readerCount == 1)
            {
                await _writerSemaphore.WaitAsync();
            }
        }
        finally
        {
            _readerSemaphore.Release();
        }

        return new Releaser(this, false);
    }

    /// <summary>
    /// 带超时的异步获取读锁
    /// </summary>
    /// <param name="timeout">获取读锁的总超时时间，<see cref="Timeout.InfiniteTimeSpan"/> 表示不限时</param>
    /// <returns>释放后即解除读锁的句柄</returns>
    /// <exception cref="TimeoutException">超时未获取到读锁时抛出</exception>
    /// <remarks>
    /// 超时是整体预算：内部两段等待共享同一份时间，最坏等待不会超过 <paramref name="timeout"/>。
    /// 超时抛出时读者计数会回滚，锁状态与调用前一致。
    /// </remarks>
    public async Task<IDisposable> AcquireReadLockAsync(TimeSpan timeout)
    {
        var startTimestamp = Stopwatch.GetTimestamp();

        if (!await _readerSemaphore.WaitAsync(timeout))
        {
            throw new TimeoutException("未能在超时时间内获取读锁。");
        }

        var acquired = false;
        try
        {
            _readerCount++;
            if (_readerCount == 1)
            {
                // 第一个读者负责占住写信号量以挡住写者。
                // 这里必须用剩余时间而不是完整的 timeout，否则最坏要等两倍超时。
                if (!await _writerSemaphore.WaitAsync(GetRemainingTimeout(timeout, startTimestamp)))
                {
                    throw new TimeoutException("未能在超时时间内获取写锁以阻止写入。");
                }
            }

            acquired = true;
        }
        finally
        {
            // 原实现在超时抛出时只放掉了 _readerSemaphore，_readerCount 停在已自增的值上：
            // 之后每个读者都看到计数不为 1，再也不会去占写信号量，
            // 读锁与写锁从此可以同时持有，互斥彻底失效且无法自愈。
            // 因此任何未成功获取的路径都必须把计数回滚。
            if (!acquired)
            {
                _readerCount--;
            }

            _readerSemaphore.Release();
        }

        return new Releaser(this, false);
    }

    /// <summary>
    /// 计算距离超时还剩多少时间
    /// </summary>
    /// <param name="timeout">总超时时间，<see cref="Timeout.InfiniteTimeSpan"/> 表示不限时</param>
    /// <param name="startTimestamp">开始等待时的时间戳</param>
    /// <returns>剩余可等待时间，已耗尽时为 <see cref="TimeSpan.Zero"/></returns>
    private static TimeSpan GetRemainingTimeout(TimeSpan timeout, long startTimestamp)
    {
        if (timeout == Timeout.InfiniteTimeSpan)
        {
            return timeout;
        }

        var remaining = timeout - Stopwatch.GetElapsedTime(startTimestamp);
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    #endregion 读锁

    #region 写锁

    /// <summary>
    /// 异步获取写锁
    /// </summary>
    /// <returns></returns>
    public async Task<IDisposable> AcquireWriteLockAsync()
    {
        await _writerSemaphore.WaitAsync();
        return new Releaser(this, true);
    }

    /// <summary>
    /// 带超时的异步获取写锁
    /// </summary>
    /// <param name="timeout"></param>
    /// <returns></returns>
    /// <exception cref="TimeoutException"></exception>
    public async Task<IDisposable> AcquireWriteLockAsync(TimeSpan timeout)
    {
        return !await _writerSemaphore.WaitAsync(timeout) ? throw new TimeoutException("未能在超时时间内获取写锁。") : (IDisposable)new Releaser(this, true);
    }

    #endregion 写锁

    #region 释放器

    private void ReleaseReadLock()
    {
        _readerSemaphore.Wait();
        try
        {
            if (--_readerCount == 0)
            {
                _writerSemaphore.Release();
            }
        }
        finally
        {
            _readerSemaphore.Release();
        }
    }

    private void ReleaseWriteLock()
    {
        _writerSemaphore.Release();
    }

    private class Releaser : IDisposable
    {
        private readonly AsyncReaderWriterLock _lock;
        private readonly bool _isWriter;
        private bool _disposed;

        public Releaser(AsyncReaderWriterLock lockObj, bool isWriter)
        {
            _lock = lockObj;
            _isWriter = isWriter;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_isWriter)
            {
                _lock.ReleaseWriteLock();
            }
            else
            {
                _lock.ReleaseReadLock();
            }

            _disposed = true;
        }
    }

    #endregion 释放器
}
