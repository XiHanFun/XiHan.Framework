#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EventHandlerFactoryUnregistrar
// Guid:ada4f87c-43f8-4044-94ea-d3eb86bdf52b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 5:16:24
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.EventBus.Abstractions;

namespace XiHan.Framework.EventBus;

/// <summary>
/// 事件处理器工厂注销器
/// </summary>
public class EventHandlerFactoryUnregistrar : IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly Type _eventType;
    private readonly IEventHandlerFactory _factory;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="eventBus">事件总线</param>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理器工厂</param>
    public EventHandlerFactoryUnregistrar(IEventBus eventBus, Type eventType, IEventHandlerFactory factory)
    {
        _eventBus = eventBus;
        _eventType = eventType;
        _factory = factory;
    }

    /// <summary>
    /// 释放
    /// </summary>
    public void Dispose()
    {
        _eventBus.Unsubscribe(_eventType, _factory);
    }
}
