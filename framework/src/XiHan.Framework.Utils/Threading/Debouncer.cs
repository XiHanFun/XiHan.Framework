// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Threading;

/// <summary>
/// 防抖器(用于防止频繁触发事件)
/// </summary>
public class Debouncer : IDisposable
{
    private readonly TimeSpan _interval;
    private readonly Lock _locker = new();
    private CancellationTokenSource? _cts;

    /// <summary>
    /// 初始化防抖器
    /// </summary>
    /// <param name="interval"></param>
    public Debouncer(TimeSpan interval)
    {
        _interval = interval;
    }

    /// <summary>
    /// 执行防抖操作
    /// </summary>
    /// <param name="action">要执行的操作</param>
    /// <remarks>在指定间隔内重复调用会取消前次操作</remarks>
    public void Debounce(Action action)
    {
        lock (_locker)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            Task.Delay(_interval, _cts.Token)
                .ContinueWith(t =>
                {
                    if (!t.IsCanceled)
                    {
                        action();
                    }
                });
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
