// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Uow.Abstracts;

/// <summary>
/// 工作单元事件发布者
/// </summary>
public interface IUnitOfWorkEventPublisher
{
    /// <summary>
    /// 发布本地事件
    /// </summary>
    /// <param name="localEvents"></param>
    /// <returns></returns>
    Task PublishLocalEventsAsync(IEnumerable<UnitOfWorkEventRecord> localEvents);

    /// <summary>
    /// 发布分布式事件
    /// </summary>
    /// <param name="distributedEvents"></param>
    /// <returns></returns>
    Task PublishDistributedEventsAsync(IEnumerable<UnitOfWorkEventRecord> distributedEvents);
}
