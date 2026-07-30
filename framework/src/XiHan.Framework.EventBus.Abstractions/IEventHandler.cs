// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;

namespace XiHan.Framework.EventBus.Abstractions;

/// <summary>
/// 所有事件处理器的间接基础接口
/// 需要实现 <see cref="ILocalEventHandler{TEvent}"/> 或 <see cref="IDistributedEventHandler{TEvent}"/>，而不是直接实现此接口
/// </summary>
public interface IEventHandler
{
}
