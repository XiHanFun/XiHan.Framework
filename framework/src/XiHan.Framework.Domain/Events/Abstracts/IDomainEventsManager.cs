// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Domain.Events.Abstracts;

/// <summary>
/// 领域事件管理器接口
/// </summary>
public interface IDomainEventsManager
{
    /// <summary>
    /// 获取本地事件
    /// </summary>
    /// <returns>本地事件集合</returns>
    IEnumerable<DomainEventRecord> GetLocalEvents();

    /// <summary>
    /// 获取分布式事件
    /// </summary>
    /// <returns>分布式事件集合</returns>
    IEnumerable<DomainEventRecord> GetDistributedEvents();

    /// <summary>
    /// 清空本地事件
    /// </summary>
    void ClearLocalEvents();

    /// <summary>
    /// 清空分布式事件
    /// </summary>
    void ClearDistributedEvents();
}
