#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AsyncReaderWriterLock
// Guid:60dab369-bb2a-4d8e-a89b-0801b4bd9eb1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/9 6:23:28
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
            _ = _readerSemaphore.Release();
        }

        return new Releaser(this, false);
    }

    /// <summary>
    /// 带超时的异步获取读锁
    /// </summary>
    /// <param name="timeout"></param>
    /// <returns></returns>
    /// <exception cref="TimeoutException"></exception>
    public async Task<IDisposable> AcquireReadLockAsync(TimeSpan timeout)
    {
        if (!await _readerSemaphore.WaitAsync(timeout))
        {
            throw new TimeoutException("未能在超时时间内获取读锁。");
        }

        try
        {
            if (++_readerCount == 1)
            {
                if (!await _writerSemaphore.WaitAsync(timeout))
                {
                    throw new TimeoutException("未能在超时时间内获取写锁以阻止写入。");
                }
            }
        }
        finally
        {
            _ = _readerSemaphore.Release();
        }

        return new Releaser(this, false);
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
                _ = _writerSemaphore.Release();
            }
        }
        finally
        {
            _ = _readerSemaphore.Release();
        }
    }

    private void ReleaseWriteLock()
    {
        _ = _writerSemaphore.Release();
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
