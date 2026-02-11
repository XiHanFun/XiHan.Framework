#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EventHandlerMethodExecutor
// Guid:e4a16cee-caeb-441a-af59-fe4c16b5ed6f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 07:35:13
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.Utils.Objects;

namespace XiHan.Framework.EventBus;

/// <summary>
/// 事件处理器方法执行器
/// </summary>
/// <param name="target"></param>
/// <param name="parameter"></param>
/// <returns></returns>
public delegate Task EventHandlerMethodExecutorAsync(IEventHandler target, object parameter);

/// <summary>
/// 事件处理器方法执行器接口
/// </summary>
public interface IEventHandlerMethodExecutor
{
    /// <summary>
    /// 异步执行器
    /// </summary>
    EventHandlerMethodExecutorAsync ExecutorAsync { get; }
}

/// <summary>
/// 本地事件处理器方法执行器
/// </summary>
/// <typeparam name="TEvent"></typeparam>
public class LocalEventHandlerMethodExecutor<TEvent> : IEventHandlerMethodExecutor
    where TEvent : class
{
    /// <summary>
    /// 本地事件处理器方法执行器
    /// </summary>
    public EventHandlerMethodExecutorAsync ExecutorAsync => (target, parameter) =>
        target.As<ILocalEventHandler<TEvent>>().HandleEventAsync(parameter.As<TEvent>());

    /// <summary>
    /// 执行事件处理
    /// </summary>
    /// <param name="target"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    public Task ExecuteAsync(IEventHandler target, TEvent parameters)
    {
        return ExecutorAsync(target, parameters);
    }
}

/// <summary>
/// 分布式事件处理器方法执行器
/// </summary>
/// <typeparam name="TEvent"></typeparam>
public class DistributedEventHandlerMethodExecutor<TEvent> : IEventHandlerMethodExecutor
    where TEvent : class
{
    /// <summary>
    /// 分布式事件处理器方法执行器
    /// </summary>
    public EventHandlerMethodExecutorAsync ExecutorAsync => (target, parameter) =>
        target.As<IDistributedEventHandler<TEvent>>().HandleEventAsync(parameter.As<TEvent>());

    /// <summary>
    /// 执行事件处理
    /// </summary>
    /// <param name="target"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    public Task ExecuteAsync(IEventHandler target, TEvent parameters)
    {
        return ExecutorAsync(target, parameters);
    }
}
