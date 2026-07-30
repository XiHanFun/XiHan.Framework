// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.Abstractions;

/// <summary>
/// 事件名称提供器接口
/// </summary>
public interface IEventNameProvider
{
    /// <summary>
    /// 获取事件名称
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <returns>事件名称</returns>
    string GetName(Type eventType);
}
