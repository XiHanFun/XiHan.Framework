#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UnitOfWorkEventPublisher
// Guid:6ee5528c-c75d-4a42-b3fc-d37e7785558b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/02 06:46:39
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Abstracts;

namespace XiHan.Framework.EventBus;

/// <summary>
/// 工作单元事件发布者
/// </summary>
[Dependency(ReplaceServices = true)]
public class UnitOfWorkEventPublisher : IUnitOfWorkEventPublisher, ITransientDependency
{
    private readonly ILocalEventBus _localEventBus;
    private readonly IDistributedEventBus _distributedEventBus;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="localEventBus"></param>
    /// <param name="distributedEventBus"></param>
    public UnitOfWorkEventPublisher(ILocalEventBus localEventBus, IDistributedEventBus distributedEventBus)
    {
        _localEventBus = localEventBus;
        _distributedEventBus = distributedEventBus;
    }

    /// <summary>
    /// 发布本地事件
    /// </summary>
    /// <param name="localEvents"></param>
    /// <returns></returns>
    public async Task PublishLocalEventsAsync(IEnumerable<UnitOfWorkEventRecord> localEvents)
    {
        foreach (var localEvent in localEvents)
        {
            await _localEventBus.PublishAsync(
                localEvent.EventType,
                localEvent.EventData,
                onUnitOfWorkComplete: false
            );
        }
    }

    /// <summary>
    /// 发布分布式事件
    /// </summary>
    /// <param name="distributedEvents"></param>
    /// <returns></returns>
    public async Task PublishDistributedEventsAsync(IEnumerable<UnitOfWorkEventRecord> distributedEvents)
    {
        foreach (var distributedEvent in distributedEvents)
        {
            await _distributedEventBus.PublishAsync(
                distributedEvent.EventType,
                distributedEvent.EventData,
                onUnitOfWorkComplete: false,
                useOutbox: distributedEvent.UseOutbox
            );
        }
    }
}
