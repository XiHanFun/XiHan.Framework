// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Events.Abstracts;

namespace XiHan.Framework.Domain.Events;

/// <summary>
/// 领域事件记录
/// </summary>
public class DomainEventRecord
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <param name="eventOrder">事件顺序</param>
    public DomainEventRecord(IDomainEvent eventData, long eventOrder)
    {
        EventData = eventData;
        EventOrder = eventOrder;
    }

    /// <summary>
    /// 事件数据
    /// </summary>
    public IDomainEvent EventData { get; }

    /// <summary>
    /// 事件顺序
    /// </summary>
    public long EventOrder { get; }
}
