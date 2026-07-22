// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Threading;

/// <summary>
/// 在调用释放方法时提供一个操作
/// </summary>
public class DisposeAction : IDisposable
{
    private readonly Action _action;

    /// <summary>
    /// 创建新的<see cref="DisposeAction"/>对象
    /// </summary>
    /// <param name="action">此对象被处理时所执行的动作</param>
    public DisposeAction(Action action)
    {
        ArgumentNullException.ThrowIfNull(action, nameof(action));

        _action = action;
    }

    /// <summary>
    /// 释放
    /// </summary>
    public void Dispose()
    {
        _action();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 在调用释放方法时提供一个操作
/// </summary>
/// <typeparam name="T"></typeparam>
public class DisposeAction<T> : IDisposable
{
    private readonly Action<T> _action;

    private readonly T? _parameter;

    /// <summary>
    /// 创建新的<see cref="DisposeAction"/>对象
    /// </summary>
    /// <param name="action">此对象被处理时所执行的动作</param>
    /// <param name="parameter">动作的参数</param>
    public DisposeAction(Action<T> action, T parameter)
    {
        ArgumentNullException.ThrowIfNull(action, nameof(action));

        _action = action;
        _parameter = parameter;
    }

    /// <summary>
    /// 释放
    /// </summary>
    public void Dispose()
    {
        if (_parameter is not null)
        {
            _action(_parameter);
        }

        GC.SuppressFinalize(this);
    }
}
