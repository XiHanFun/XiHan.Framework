// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions;

namespace XiHan.Framework.EventBus;

/// <summary>
/// 事件处理器包装器，用于管理事件处理器的生命周期
/// </summary>
public class EventHandlerDisposeWrapper : IEventHandlerDisposeWrapper
{
    private readonly Action? _disposeAction;

    private bool _disposed;

    /// <summary>
    /// 初始化事件处理器包装器
    /// </summary>
    /// <param name="eventHandler">事件处理器</param>
    /// <param name="disposeAction">释放时执行的操作</param>
    public EventHandlerDisposeWrapper(IEventHandler eventHandler, Action? disposeAction = null)
    {
        _disposeAction = disposeAction;
        EventHandler = eventHandler;
    }

    /// <summary>
    /// 事件处理器
    /// </summary>
    public IEventHandler EventHandler { get; }

    /// <summary>
    /// 释放资源
    /// </summary>
    /// <remarks>
    /// 原来没有幂等保护，重复调用会重复执行归还动作。当前各工厂传入的动作（scope.Dispose、
    /// (handler as IDisposable)?.Dispose）自身幂等，所以还没暴露问题，但 IDisposable 的契约是
    /// 「多次 Dispose 与一次等价」，包装器不能把这个前提转嫁给调用方传入的任意动作。
    /// 先置标记再执行，动作自身抛异常时也不会被重跑。
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _disposeAction?.Invoke();
    }
}
