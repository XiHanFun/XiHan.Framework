// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Threading;

/// <summary>
/// 异步释放函数
/// </summary>
public class AsyncDisposeFunc : IAsyncDisposable
{
    private readonly Func<Task> _func;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="func">此对象在执行 DisposeAsync 时要执行的函数</param>
    public AsyncDisposeFunc(Func<Task> func)
    {
        ArgumentNullException.ThrowIfNull(func, nameof(func));

        _func = func;
    }

    /// <summary>
    /// 释放
    /// </summary>
    /// <returns></returns>
    public async ValueTask DisposeAsync()
    {
        await _func();

        GC.SuppressFinalize(this);
    }
}
