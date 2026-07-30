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
    public void Dispose()
    {
        _disposeAction?.Invoke();
    }
}
