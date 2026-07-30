// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.Abstractions;

/// <summary>
/// 事件处理器工厂接口
/// </summary>
public interface IEventHandlerFactory
{
    /// <summary>
    /// 获取一个事件处理器
    /// </summary>
    /// <returns>事件处理器包装对象</returns>
    IEventHandlerDisposeWrapper GetHandler();

    /// <summary>
    /// 判断当前工厂是否存在于指定的事件处理器工厂列表中
    /// </summary>
    /// <param name="handlerFactories">事件处理器工厂列表</param>
    /// <returns>如果存在则返回 true</returns>
    bool IsInFactories(List<IEventHandlerFactory> handlerFactories);
}
