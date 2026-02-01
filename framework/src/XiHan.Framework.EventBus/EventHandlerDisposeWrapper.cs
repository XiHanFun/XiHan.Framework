#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EventHandlerDisposeWrapper
// Guid:9576f207-a88a-4eeb-a6b4-86bd0c608693
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/05 08:13:11
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
