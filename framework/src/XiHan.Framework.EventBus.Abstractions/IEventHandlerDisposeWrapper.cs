// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.Abstractions;

/// <summary>
/// 事件处理器包装接口
/// </summary>
public interface IEventHandlerDisposeWrapper : IDisposable
{
    /// <summary>
    /// 获取事件处理器实例
    /// </summary>
    IEventHandler EventHandler { get; }
}
