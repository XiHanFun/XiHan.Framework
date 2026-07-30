// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.Abstractions.Local;

/// <summary>
/// 本地事件处理器接口
/// </summary>
public interface ILocalEventHandler<in TEvent> : IEventHandler
{
    /// <summary>
    /// 事件处理器通过实现此方法来处理事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    Task HandleEventAsync(TEvent eventData);
}
