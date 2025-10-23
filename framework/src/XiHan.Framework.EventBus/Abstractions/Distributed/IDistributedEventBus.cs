#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IDistributedEventBus
// Guid:87f72a8f-6789-45ce-8bad-186702184d30
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/2 7:02:09
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.EventBus.Abstractions.Distributed;

/// <summary>
/// 分布式事件总线接口
/// </summary>
public interface IDistributedEventBus : IEventBus
{
    /// <summary>
    /// 订阅事件
    /// 所有事件发生时都使用相同的(给定的)事件处理器实例
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">用于处理事件的对象</param>
    IDisposable Subscribe<TEvent>(IDistributedEventHandler<TEvent> handler)
        where TEvent : class;

    /// <summary>
    /// 发布分布式事件(泛型方式)
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="eventData">事件数据</param>
    /// <param name="onUnitOfWorkComplete">是否在当前工作单元完成后再发布事件</param>
    /// <param name="useOutbox">是否使用收件箱模式进行事件持久化与可靠投递</param>
    Task PublishAsync<TEvent>(TEvent eventData, bool onUnitOfWorkComplete = true, bool useOutbox = true)
        where TEvent : class;

    /// <summary>
    /// 发布分布式事件(非泛型方式)
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <param name="onUnitOfWorkComplete">是否在当前工作单元完成后再发布事件</param>
    /// <param name="useOutbox">是否使用收件箱模式进行事件持久化与可靠投递</param>
    Task PublishAsync(Type eventType, object eventData, bool onUnitOfWorkComplete = true, bool useOutbox = true);
}
