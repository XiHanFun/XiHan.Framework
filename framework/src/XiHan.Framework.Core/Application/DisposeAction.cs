#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DisposeAction
// Guid:99e15063-fc1a-4d4a-8d28-788cdd49933e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/28 3:40:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Core.Application;

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
    public DisposeAction([NotNull] Action action)
    {
        CheckHelper.NotNull(action, nameof(action));

        _action = action;
    }

    /// <summary>
    /// 释放
    /// </summary>
    public void Dispose()
    {
        _action();
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
        CheckHelper.NotNull(action, nameof(action));

        _action = action;
        _parameter = parameter;
    }

    /// <summary>
    /// 释放
    /// </summary>
    public void Dispose()
    {
        if (_parameter != null)
        {
            _action(_parameter);
        }
    }
}
