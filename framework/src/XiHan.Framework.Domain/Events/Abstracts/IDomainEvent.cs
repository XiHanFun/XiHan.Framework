// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Domain.Events.Abstracts;

/// <summary>
/// 领域事件接口
/// 标记接口，用于标识领域事件
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// 事件唯一标识
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// 事件发生时间
    /// </summary>
    DateTimeOffset OccurredOn { get; }
}
