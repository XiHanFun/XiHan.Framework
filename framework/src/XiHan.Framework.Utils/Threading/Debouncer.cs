// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Threading;

/// <summary>
/// 防抖器(用于防止频繁触发事件)
/// </summary>
/// <remarks>
/// 每次 <see cref="Debounce"/> 都会取消上一次尚未到期的操作，只有静默满一个间隔才真正执行。
/// 操作在线程池上异步执行，<see cref="Debounce"/> 本身不等待它完成。
/// </remarks>
public class Debouncer : IDisposable
{
    private readonly TimeSpan _interval;
    private readonly Lock _locker = new();
    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>
    /// 初始化防抖器
    /// </summary>
    /// <param name="interval">静默间隔，最后一次触发后需要静默这么久才执行操作</param>
    public Debouncer(TimeSpan interval)
    {
        _interval = interval;
    }

    /// <summary>
    /// 执行防抖操作
    /// </summary>
    /// <param name="action">要执行的操作</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> 为 null 时抛出</exception>
    /// <remarks>
    /// 在指定间隔内重复调用会取消前次操作。
    /// <para>
    /// 释放之后调用不抛异常，直接忽略：防抖器的典型用法是挂在文件监听、输入变更这类异步回调上，
    /// 回调线程与释放线程之间天然存在竞态——调用方刚判断完未释放，释放就可能在下一行发生。
    /// 让这种竞态抛异常等于把异常抛进别人的回调栈（框架内 VirtualFileSystem 的文件变更回调就是这个形态），
    /// 因此把"释放后不再接受新操作"定义成正常语义而不是错误。
    /// 原实现在这种情况下会从内部的 CancellationTokenSource 抛出 ObjectDisposedException。
    /// </para>
    /// </remarks>
    public void Debounce(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        CancellationToken token;

        lock (_locker)
        {
            if (_disposed)
            {
                return;
            }

            // 取消并释放上一次的令牌源：原实现只取消不释放，每调用一次就泄漏一个
            _cts?.Cancel();
            _cts?.Dispose();

            _cts = new CancellationTokenSource();
            token = _cts.Token;
        }

        _ = Task.Delay(_interval, token).ContinueWith(
            task =>
            {
                if (!task.IsCanceled)
                {
                    action();
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    /// <remarks>取消尚未到期的操作；已经开始执行的操作不会被中断。重复释放安全。</remarks>
    public void Dispose()
    {
        lock (_locker)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        GC.SuppressFinalize(this);
    }
}
